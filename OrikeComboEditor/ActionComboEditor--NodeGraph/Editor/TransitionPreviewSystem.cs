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
    ///   LeadIn（来源动作播到自动触发点） -> Fade（按配置混合）
    ///   -> Hold（目标动作停留） -> 自动循环
    ///
    /// 混合过程中直接读取 TransitionData，
    /// Inspector 中实时修改任意过渡参数（含归一化 / 秒模式）会立即生效。
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

        /// <summary>
        /// 没有连线数据时的默认等待时长（秒）。
        /// </summary>
        private const float DefaultLeadInDuration =
            0.8f;

        private const float HoldDuration =
            1.2f;


        // =========================================================
        // 预览对象
        // =========================================================

        /// <summary>
        /// 原始角色对象（直接使用，不复制）。
        /// </summary>
        private GameObject _instance;

        private Animator _animator;

        /// <summary>
        /// 预览开始时保存的原始 Animator 状态，
        /// 预览结束后恢复。
        /// </summary>
        private bool _savedEnabled;

        private RuntimeAnimatorController _savedController;

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

        /// <summary>
        /// 进入 Fade 后真实流逝的时间（不受 TransitionTime 跳过部分影响），
        /// 用于推进两个 Clip 的采样时间。
        /// </summary>
        private float _fadeElapsed;

        private float _holdTime;

        /// <summary>
        /// 来源动作是否循环：循环时 LeadIn / Fade 期间循环取帧，
        /// 非循环时停在末尾（与运行时行为一致）。
        /// </summary>
        private bool _fromLoop =
            true;


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

            _fromLoop =
                true;

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

            // 来源动作循环时 LeadIn 用固定展示时长，
            // 非循环的自动转移按“触发点”实时计算（见 GetLeadInThreshold）
            _fromLoop =
                transition == null ||
                transition.From == null ||
                transition.From.Loop;

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

            // 目标动作先定位到配置的起始点（归一化 / 秒均换算为秒）
            _p1.SetTime(
                GetStartOffsetSeconds());

            // 过渡从配置的“自身起始进度”直接开始
            float fadeDuration =
                GetFadeDuration();

            float transitionTime =
                GetTransitionTime();

            _fadeTime =
                transitionTime *
                fadeDuration;

            _fadeElapsed =
                0f;

            SetWeights(
                1f - transitionTime,
                transitionTime);

            _time =
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
                GetLeadInThreshold())
            {
                _fadeTime =
                    GetTransitionTime() *
                    GetFadeDuration();

                _fadeElapsed =
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

            _fadeElapsed +=
                deltaTime;

            // 权重不低于配置的过渡起始进度，
            // 拖动 TransitionTime 滑条时可实时看到效果
            float weightIn =
                Mathf.Max(
                    GetTransitionTime(),
                    Mathf.Clamp01(
                        _fadeTime /
                        GetFadeDuration()));

            ApplyNormalizedWeights(
                1f - weightIn,
                weightIn);

            UpdateFromClipTime();

            UpdateToClipTime();

            if (weightIn >= 1f)
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

            if (length <= 0f)
            {
                return;
            }

            float time =
                _phase == Phase.Fade
                    ? GetLeadInThreshold() + _fadeElapsed
                    : _time;

            // 来源动作循环时循环取帧；
            // 非循环时停在末尾，与运行时的自动触发行为一致
            time =
                _fromLoop
                    ? Mathf.Repeat(
                        time,
                        length)
                    : Mathf.Clamp(
                        time,
                        0f,
                        length);

            _p0.SetTime(
                time);
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
                GetStartOffsetSeconds();

            // 从目标起点开始按真实时间推进，
            // 不被 TransitionTime 跳过的过渡部分快进
            float time =
                start +
                _fadeElapsed;

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

        /// <summary>
        /// 实际混合时长（秒）。
        /// 归一化模式 = TransitionDuration × 来源 Clip 长度；
        /// 秒模式直接取秒。
        /// </summary>
        private float GetFadeDuration()
        {
            if (_transition != null &&
                _transition.UseFixedTime)
            {
                return Mathf.Max(
                    0.0001f,
                    _transition.TransitionDuration);
            }

            float normalized =
                _transition != null
                    ? Mathf.Clamp01(
                        _transition.TransitionDuration)
                    : 0.25f;

            float length =
                _hasP0
                    ? _p0.GetAnimationClip().length
                    : 0f;

            return Mathf.Max(
                0.0001f,
                normalized *
                length);
        }


        /// <summary>
        /// 目标动画起始点（秒）。
        /// 归一化模式 = TimeOffset × 目标 Clip 长度；
        /// 秒模式直接取秒。
        /// </summary>
        private float GetStartOffsetSeconds()
        {
            if (!_hasP1)
            {
                return 0f;
            }

            float length =
                _p1.GetAnimationClip().length;

            if (_transition == null)
            {
                return 0f;
            }

            float seconds =
                _transition.UseFixedTime
                    ? Mathf.Max(
                        0f,
                        _transition.TimeOffset)
                    : Mathf.Clamp01(
                        _transition.TimeOffset) *
                      length;

            return Mathf.Clamp(
                seconds,
                0f,
                length);
        }


        /// <summary>
        /// 过渡自身的起始进度（0 ~ 1，始终归一化）。
        /// </summary>
        private float GetTransitionTime()
        {
            return _transition != null
                ? Mathf.Clamp01(
                    _transition.TransitionTime)
                : 0f;
        }


        /// <summary>
        /// LeadIn 持续时长（秒）。
        ///
        /// 非 Loop 来源的自动转移：来源结束时刻 - 剩余混合时长，
        /// 与运行时 GetAutoTriggerTime 的计算保持一致；
        /// 其余情况（Tag 取消预览 / Loop 来源）使用固定展示时长。
        /// </summary>
        private float GetLeadInThreshold()
        {
            if (_transition == null ||
                !_transition.Auto ||
                _fromLoop ||
                !_hasP0)
            {
                return DefaultLeadInDuration;
            }

            float sourceLength =
                _p0.GetAnimationClip().length;

            float threshold =
                sourceLength -
                (1f - GetTransitionTime()) *
                GetFadeDuration();

            return Mathf.Clamp(
                threshold,
                0f,
                sourceLength);
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
                return;
            }

            // 切换角色时先恢复旧角色
            RestoreInstance();

            // 直接使用原始角色对象，不复制
            _instance =
                character;

            _sourceCharacter =
                character;

            EnsureAnimator();

            // 预览图的输出目标切换到当前 Animator
            if (_graphCreated &&
                _graph.IsValid())
            {
                AnimationPlayableOutput output =
                    (AnimationPlayableOutput)
                        _graph.GetOutputByType<AnimationPlayableOutput>(
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
            if (_instance == null)
            {
                return;
            }

            _animator =
                _instance.GetComponent<Animator>();

            if (_animator == null)
            {
                _animator =
                    _instance.AddComponent<Animator>();
            }

            // 保存原始 Animator 状态，预览结束后恢复
            _savedEnabled =
                _animator.enabled;

            _savedController =
                _animator.runtimeAnimatorController;

            // 预览期间禁用原 Animator 的控制器，
            // 由 PlayableGraph 接管
            _animator.enabled =
                true;

            _animator.runtimeAnimatorController =
                null;
        }


        /// <summary>
        /// 恢复角色对象到预览前的状态。
        /// </summary>
        private void RestoreInstance()
        {
            if (_instance != null &&
                _animator != null)
            {
                _animator.runtimeAnimatorController =
                    _savedController;

                _animator.enabled =
                    _savedEnabled;
            }

            _instance =
                null;

            _animator =
                null;

            _savedController =
                null;
        }


        private void DestroyInstance()
        {
            RestoreInstance();
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
