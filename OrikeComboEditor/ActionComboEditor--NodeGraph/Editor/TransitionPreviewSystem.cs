using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEditor.Experimental.GraphView;


namespace Orike.ActionGraph
{
    /// <summary>
    /// Transition 过渡预览（编辑器专用）。
    ///
    /// 在临时角色实例上用手动 PlayableGraph + AnimationMixer
    /// 混合“当前动作 -> 目标动作”两个 ActionData 的动画：
    ///   LeadIn（当前动作循环） -> Fade（按 FadeOut/FadeIn 混合）
    ///   -> Hold（目标动作停留） -> 自动循环
    ///
    /// 混合过程中直接读取 TransitionData，
    /// Inspector 中实时修改 FadeOut / FadeIn / StartPercent 会立即生效。
    /// </summary>
    public class TransitionPreviewSystem
    {
        // =========================================================
        // 阶段
        // =========================================================

        private enum Phase
        {
            None,

            Single,

            LeadIn,

            Fade,

            Hold,
        }


        // =========================================================
        // 时间参数
        // =========================================================

        private const float LeadInDuration =
            0.8f;

        private const float HoldDuration =
            1.2f;


        // =========================================================
        // 预览对象
        // =========================================================

        private GameObject _instance;

        private Animator _animator;

        private PlayableGraph _graph;

        private AnimationMixerPlayable _mixer;

        private bool _graphCreated;


        // =========================================================
        // Playable
        // =========================================================

        private AnimationClipPlayable _p0;

        private AnimationClipPlayable _p1;

        private bool _hasP0;

        private bool _hasP1;


        // =========================================================
        // 数据与状态
        // =========================================================

        private ActionData _from;

        private ActionData _to;

        private TransitionData _transition;

        private GameObject _sourceCharacter;

        private Phase _phase =
            Phase.None;

        private float _time;

        private float _fadeTime;

        private float _holdTime;


        /// <summary>
        /// 是否正在预览。
        /// </summary>
        public bool IsRunning =>
            _phase != Phase.None;


        /// <summary>
        /// 当前预览的实例角色。
        /// </summary>
        public GameObject PreviewInstance =>
            _instance;


        // =========================================================
        // 单动作预览
        // =========================================================

        public void StartSingle(
            GameObject character,
            ActionData data)
        {
            if (character == null ||
                data == null)
            {
                return;
            }

            AnimationClip clip =
                ActionDataUtility.GetFirstAnimationClip(
                    data);

            if (clip == null)
            {
                Debug.LogWarning(
                    "[TransitionPreview] ActionData 中没有动画 Clip，无法预览。");

                return;
            }

            EnsureGraph();

            SetupInstance(
                character);

            ResetPlayables();

            _from =
                data;

            _to =
                null;

            _transition =
                null;

            _p0 =
                CreatePausedClipPlayable(
                    clip);

            _hasP0 =
                true;

            _graph.Connect(
                _p0,
                0,
                _mixer,
                0);

            SetWeights(
                1f,
                0f);

            _time =
                0f;

            _phase =
                Phase.Single;

            EvaluateGraph(
                0f);

            FocusSceneView();
        }


        // =========================================================
        // 过渡预览
        // =========================================================

        public void StartTransition(
            GameObject character,
            ActionData from,
            ActionData to,
            TransitionData transition)
        {
            if (character == null)
            {
                Debug.LogWarning(
                    "[TransitionPreview] 未指定预览角色。");

                return;
            }

            if (from == null ||
                to == null)
            {
                Debug.LogWarning(
                    "[TransitionPreview] From / To 的 ActionData 为空，无法预览。");

                return;
            }

            AnimationClip clip0 =
                ActionDataUtility.GetFirstAnimationClip(
                    from);

            AnimationClip clip1 =
                ActionDataUtility.GetFirstAnimationClip(
                    to);

            if (clip0 == null ||
                clip1 == null)
            {
                Debug.LogWarning(
                    "[TransitionPreview] From / To 的 ActionData 缺少动画 Clip，无法预览。");

                return;
            }

            EnsureGraph();

            SetupInstance(
                character);

            ResetPlayables();

            _sourceCharacter =
                character;

            _from =
                from;

            _to =
                to;

            _transition =
                transition;

            _p0 =
                CreatePausedClipPlayable(
                    clip0);

            _p1 =
                CreatePausedClipPlayable(
                    clip1);

            _hasP0 =
                true;

            _hasP1 =
                true;

            _graph.Connect(
                _p0,
                0,
                _mixer,
                0);

            _graph.Connect(
                _p1,
                0,
                _mixer,
                1);

            // 目标动作先定位到 StartPercent
            _p1.SetTime(
                GetStartPercent() *
                clip1.length);

            SetWeights(
                1f,
                0f);

            _time =
                0f;

            _fadeTime =
                0f;

            _holdTime =
                0f;

            _phase =
                Phase.LeadIn;

            EvaluateGraph(
                0f);

            FocusSceneView();
        }


        /// <summary>
        /// 停止预览并销毁临时角色。
        /// </summary>
        public void Stop()
        {
            _phase =
                Phase.None;

            ResetPlayables();

            DestroyInstance();
        }


        /// <summary>
        /// 窗口关闭时彻底释放。
        /// </summary>
        public void Dispose()
        {
            Stop();

            if (_graphCreated &&
                _graph.IsValid())
            {
                _graph.Destroy();
            }

            _graphCreated =
                false;
        }


        // =========================================================
        // 每帧驱动（由 EditorApplication.update 调用）
        // =========================================================

        public void Tick(
            float deltaTime)
        {
            if (_phase == Phase.None ||
                !_graphCreated)
            {
                return;
            }

            switch (_phase)
            {
                case Phase.Single:

                    TickSingle(
                        deltaTime);

                    break;

                case Phase.LeadIn:

                    TickLeadIn(
                        deltaTime);

                    break;

                case Phase.Fade:

                    TickFade(
                        deltaTime);

                    break;

                case Phase.Hold:

                    TickHold(
                        deltaTime);

                    break;
            }

            EvaluateGraph(
                deltaTime);
        }


        private void TickSingle(
            float deltaTime)
        {
            if (!_hasP0)
            {
                return;
            }

            _time +=
                deltaTime;

            AnimationClip clip =
                _p0.GetAnimationClip();

            float length =
                clip.length;

            if (length > 0f)
            {
                _p0.SetTime(
                    Mathf.Repeat(
                        _time,
                        length));
            }

            SetWeights(
                1f,
                0f);
        }


        private void TickLeadIn(
            float deltaTime)
        {
            _time +=
                deltaTime;

            UpdateFromClipTime();

            SetWeights(
                1f,
                0f);

            if (_time >=
                LeadInDuration)
            {
                _fadeTime =
                    0f;

                _phase =
                    Phase.Fade;
            }
        }


        private void TickFade(
            float deltaTime)
        {
            _fadeTime +=
                deltaTime;

            float fadeOut =
                GetFadeOut();

            float fadeIn =
                GetFadeIn();

            float weightOut =
                1f -
                Mathf.Clamp01(
                    _fadeTime /
                    fadeOut);

            float weightIn =
                Mathf.Clamp01(
                    _fadeTime /
                    fadeIn);

            ApplyNormalizedWeights(
                weightOut,
                weightIn);

            UpdateFromClipTime();

            UpdateToClipTime();

            if (weightOut <= 0f &&
                weightIn >= 1f)
            {
                _holdTime =
                    0f;

                _phase =
                    Phase.Hold;
            }
        }


        private void TickHold(
            float deltaTime)
        {
            _holdTime +=
                deltaTime;

            SetWeights(
                0f,
                1f);

            UpdateToClipTime();

            if (_holdTime >=
                HoldDuration)
            {
                // 自动重播整个过渡，方便反复观察
                StartTransition(
                    _sourceCharacter,
                    _from,
                    _to,
                    _transition);
            }
        }


        // =========================================================
        // 动画时间（暂停的 Playable，手动 SetTime 精确定位）
        // =========================================================

        private void UpdateFromClipTime()
        {
            if (!_hasP0)
            {
                return;
            }

            float length =
                _p0.GetAnimationClip().length;

            if (length > 0f)
            {
                _p0.SetTime(
                    Mathf.Repeat(
                        LeadInDuration > 0f && _phase == Phase.Fade
                            ? LeadInDuration + _fadeTime
                            : _time,
                        length));
            }
        }


        private void UpdateToClipTime()
        {
            if (!_hasP1)
            {
                return;
            }

            AnimationClip clip =
                _p1.GetAnimationClip();

            float start =
                GetStartPercent() *
                clip.length;

            float elapsed =
                _fadeTime;

            float time =
                start +
                elapsed;

            if (clip.length > 0f)
            {
                time =
                    Mathf.Min(
                        time,
                        clip.length);
            }

            _p1.SetTime(
                time);
        }


        private void ApplyNormalizedWeights(
            float weightOut,
            float weightIn)
        {
            float sum =
                weightOut +
                weightIn;

            if (sum <= 0f)
            {
                SetWeights(
                    1f,
                    0f);

                return;
            }

            SetWeights(
                weightOut / sum,
                weightIn / sum);
        }


        // =========================================================
        // Transition 参数（实时读取，Inspector 改完立即生效）
        // =========================================================

        private float GetFadeOut()
        {
            float value =
                _transition != null
                    ? _transition.FadeOut
                    : 0.25f;

            return value > 0.0001f
                ? value
                : 0.0001f;
        }


        private float GetFadeIn()
        {
            float value =
                _transition != null
                    ? _transition.FadeIn
                    : 0.25f;

            return value > 0.0001f
                ? value
                : 0.0001f;
        }


        private float GetStartPercent()
        {
            return _transition != null
                ? Mathf.Clamp01(
                    _transition.StartPercent)
                : 0f;
        }


        // =========================================================
        // Graph / 实例
        // =========================================================

        private void EnsureGraph()
        {
            if (_graphCreated)
            {
                return;
            }

            _graph =
                PlayableGraph.Create(
                    "TransitionPreview");

            _graph.SetTimeUpdateMode(
                DirectorUpdateMode.Manual);

            _mixer =
                AnimationMixerPlayable.Create(
                    _graph,
                    2);

            AnimationPlayableOutput output =
                AnimationPlayableOutput.Create(
                    _graph,
                    "TransitionPreview",
                    _animator);

            output.SetSourcePlayable(
                _mixer);

            _graph.Play();

            _graphCreated =
                true;
        }


        private void SetupInstance(
            GameObject character)
        {
            if (_instance != null &&
                _sourceCharacter == character)
            {
                EnsureAnimator();

                return;
            }

            DestroyInstance();

            _instance =
                Object.Instantiate(
                    character);

            _instance.name =
                "[TransitionPreview] " +
                character.name;

            _instance.hideFlags =
                HideFlags.HideAndDontSave;

            _instance.transform.position =
                Vector3.zero;

            _sourceCharacter =
                character;

            foreach (Transform child
                     in _instance.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.hideFlags =
                    HideFlags.HideAndDontSave;
            }

            EnsureAnimator();


            // 预览图的输出目标切换到新 Animator
            if (_graphCreated)
            {
                AnimationPlayableOutput output =
                    (AnimationPlayableOutput)_graph.GetOutputByType<AnimationPlayableOutput>(
                        0);

                if (output.IsOutputValid())
                {
                    output.SetTarget(
                        _animator);
                }
            }
        }


        private void EnsureAnimator()
        {
            _animator =
                _instance.GetComponent<Animator>();

            if (_animator == null)
            {
                _animator =
                    _instance.AddComponent<Animator>();
            }
        }


        private void DestroyInstance()
        {
            if (_instance != null)
            {
                Object.DestroyImmediate(
                    _instance);

                _instance =
                    null;
            }

            _animator =
                null;
        }


        private AnimationClipPlayable CreatePausedClipPlayable(
            AnimationClip clip)
        {
            AnimationClipPlayable playable =
                AnimationClipPlayable.Create(
                    _graph,
                    clip);

            playable.Pause();

            return playable;
        }


        private void ResetPlayables()
        {
            if (_graphCreated)
            {
                DestroyPlayable(0);

                DestroyPlayable(1);
            }

            _p0 =
                default;

            _p1 =
                default;

            _hasP0 =
                false;

            _hasP1 =
                false;
        }


        private void DestroyPlayable(
            int slot)
        {
            if (!_graphCreated)
            {
                return;
            }

            Playable input =
                _mixer.GetInput(
                    slot);

            if (input.IsValid())
            {
                _graph.Disconnect(
                    _mixer,
                    slot);

                _graph.DestroyPlayable(
                    input);
            }
        }


        private void SetWeights(
            float weight0,
            float weight1)
        {
            if (!_graphCreated)
            {
                return;
            }

            _mixer.SetInputWeight(
                0,
                weight0);

            _mixer.SetInputWeight(
                1,
                weight1);
        }


        private void EvaluateGraph(
            float deltaTime)
        {
            if (_graphCreated &&
                _graph.IsValid())
            {
                _graph.Evaluate(
                    deltaTime);
            }
        }


        private void FocusSceneView()
        {
            SceneView sceneView =
                SceneView.lastActiveSceneView;

            if (sceneView != null &&
                _instance != null)
            {
                sceneView.LookAt(
                    _instance.transform.position,
                    sceneView.rotation,
                    2f,
                    false,
                    true);
            }
        }
    }
}
