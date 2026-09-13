using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// Action 播放器。
///
/// 挂在角色对象上，负责播放角色的 ActionData。
/// 播放时会按时间推进 ActionData 中的各类轨道。
///
///   - 使用 PlayableGraph 和 AnimationMixer，支持 CrossFade。
///   - Clip 开始时通过 AudioSource.PlayOneShot 播放音效。
///   - 根据当前时间创建并更新特效实例。
///   - 按 Clip 时间切换 Hitbox.activate，Collider 本身保持启用。
///   - 进入、更新和离开事件时分别触发 OnStart、OnUpdate、OnEnd。
///   - 播放时间越过事件点时触发 OnCall。
///
/// 主要接口：
///   Play(action)              立即播放动作。
///   CrossFade(action, 0.25f)  淡入播放另一个动作。
///   Stop / Pause / Resume
/// </summary>
public class ActionPlayer : MonoBehaviour
{
    // =========================================================
    // 配置
    // =========================================================

    /// <summary>
    /// 拥有这些 Action 的角色。为空时使用当前 GameObject。
    /// </summary>
    public GameObject master;

    /// <summary>
    /// 角色拥有的全部 ActionData。
    /// 也可以通过名称查找并播放，例如 Play("Idle")。
    /// </summary>
    public List<ActionData> Actions =
        new List<ActionData>();

    /// <summary>
    /// CrossFade 默认持续时间，单位为秒。
    /// </summary>
    public float DefaultCrossFadeDuration =
        0.25f;


    // =========================================================
    // 事件回调
    // =========================================================

    /// <summary>
    /// Play 或 CrossFade 开始动作后触发。
    /// </summary>
    public event Action<ActionData> ActionStarted;

    /// <summary>
    /// 非循环动作自然播放结束时触发。
    /// </summary>
    public event Action<ActionData> ActionCompleted;


    // =========================================================
    // 状态
    // =========================================================

    /// <summary>
    /// 当前正在播放的 ActionData，包含正在淡出的动作。
    /// </summary>
    public ActionData CurrentAction
    {
        get;
        private set;
    }

    /// <summary>
    /// 当前是否正在播放。
    /// </summary>
    public bool IsPlaying
    {
        get;
        private set;
    }

    /// <summary>
    /// 当前动作是否循环。
    /// </summary>
    public bool IsLooping
    {
        get;
        private set;
    }

    /// <summary>
    /// 当前动作的播放时间，单位为秒。
    /// </summary>
    public float CurrentTime
    {
        get
        {
            return _active != null
                ? _active.Time
                : 0f;
        }
    }

    /// <summary>
    /// 当前动作的持续时间，单位为秒。
    /// </summary>
    public float CurrentDuration
    {
        get
        {
            return _active != null
                ? _active.Duration
                : 0f;
        }
    }

    /// <summary>
    /// 当前动作的归一化时间，范围为 0 到 1。
    /// </summary>
    public float NormalizedTime
    {
        get
        {
            if (_active == null ||
                _active.Duration <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(
                _active.Time /
                _active.Duration);
        }
    }


    // =========================================================
    // PlayableGraph
    // =========================================================

    private PlayableGraph _graph;

    private AnimationMixerPlayable _mixer;

    private Animator _animator;

    private AudioSource _audioSource;

    private bool _graphCreated;

    private bool _paused;


    // 动画槽位：
    //   0 = 当前动画。
    //   1 = CrossFade 期间淡入的新动画。
    private AnimationClip _slot0Clip;

    private AnimationClip _slot1Clip;

    private float _slot0Weight = 1f;

    private float _slot1Weight;


    // =========================================================
    // 播放状态
    // =========================================================

    private ActionPlayback _active;

    private ActionPlayback _fadingOut;

    private float _fadeTimer;

    private float _fadeDuration;


    // =========================================================
    // 角色
    // =========================================================

    private GameObject MasterObject
    {
        get
        {
            return master != null
                ? master
                : gameObject;
        }
    }


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        EnsureGraph();
    }


    private void OnDestroy()
    {
        ShutdownGraph();
    }


    private void Update()
    {
        Tick(
            Time.deltaTime);
    }


    // =========================================================
    // 播放接口
    // =========================================================

    /// <summary>
    /// 按名称从动作列表中查找 ActionData。
    /// </summary>
    public ActionData GetAction(
        string actionName)
    {
        if (string.IsNullOrEmpty(
                actionName))
        {
            return null;
        }

        foreach (
            ActionData action
            in Actions)
        {
            if (action != null &&
                action.name == actionName)
            {
                return action;
            }
        }

        return null;
    }


    /// <summary>
    /// 立即播放指定动作。
    /// 会停止当前动作以及 CrossFade 中正在淡出的旧动作。
    /// </summary>
    public void Play(
        ActionData action,
        bool loop = false,
        float startPercent = 0f)
    {
        if (action == null)
        {
            Debug.LogWarning(
                "[ActionPlayer] 播放失败：ActionData 为空。");

            return;
        }

        EnsureGraph();

        ResumeGraph();


        // 停止并清理旧动作。
        if (_fadingOut != null)
        {
            TeardownPlayback(
                _fadingOut,
                true);

            _fadingOut =
                null;
        }

        if (_active != null)
        {
            TeardownPlayback(
                _active,
                true);

            _active =
                null;
        }

        ClearSlots();

        _active =
            CreatePlayback(
                action,
                loop);

        ApplyStartTime(
            _active,
            startPercent);

        CurrentAction =
            action;

        IsLooping =
            loop;

        IsPlaying =
            true;

        ReplaceSlotClip(
            0,
            GetAnimationClipAt(
                _active,
                _active.Time),
            GetAnimationClipLocalTime(
                _active,
                _active.Time));

        SetSlotWeights(
            1f,
            0f);

        Evaluate(
            _active,
            _active.Time);

        ActionStarted?.Invoke(
            action);
    }


    /// <summary>
    /// 按名称查找并播放动作。
    /// </summary>
    public void Play(
        string actionName,
        bool loop = false)
    {
        ActionData action =
            GetAction(
                actionName);

        if (action == null)
        {
            Debug.LogWarning(
                $"[ActionPlayer] 播放失败：找不到名为 \"{actionName}\" 的 ActionData。");

            return;
        }

        Play(
            action,
            loop);
    }


    /// <summary>
    /// 淡入播放另一个动作。
    /// fadeDuration 小于等于 0 时使用 DefaultCrossFadeDuration。
    /// </summary>
    public void CrossFade(
        ActionData action,
        float fadeDuration = -1f,
        bool loop = false,
        float startPercent = 0f)
    {
        if (action == null)
        {
            Debug.LogWarning(
                "[ActionPlayer] 淡入失败：ActionData 为空。");

            return;
        }

        if (fadeDuration <= 0f)
        {
            fadeDuration =
                DefaultCrossFadeDuration;
        }

        EnsureGraph();

        ResumeGraph();


        // 没有正在播放的动作时直接播放。
        if (!IsPlaying ||
            _active == null ||
            _active.Finished)
        {
            Play(
                action,
                loop,
                startPercent);

            return;
        }


        // 相同动作无需重复淡入。
        if (action == CurrentAction)
        {
            return;
        }


        // 如果已经处于 CrossFade 中，先清理旧的淡出动作，
        // 再将当前动画移动到槽位 0。
        if (_fadingOut != null)
        {
            TeardownPlayback(
                _fadingOut,
                true);

            _fadingOut =
                null;
        }

        if (_slot1Clip != null)
        {
            MoveSlot1ToSlot0();
        }


        // 将当前动作淡出。
        _fadingOut =
            _active;

        FadeOutLogic(
            _fadingOut);


        // 从时间 0 开始播放新动作。
        _active =
            CreatePlayback(
                action,
                loop);

        ApplyStartTime(
            _active,
            startPercent);

        CurrentAction =
            action;

        IsLooping =
            loop;

        IsPlaying =
            true;

        ReplaceSlotClip(
            1,
            GetAnimationClipAt(
                _active,
                _active.Time),
            GetAnimationClipLocalTime(
                _active,
                _active.Time));

        SetSlotWeights(
            1f,
            0f);

        _fadeTimer =
            0f;

        _fadeDuration =
            Mathf.Max(
                0.0001f,
                fadeDuration);


        // 在起始时间处理新动作的事件和片段。
        Evaluate(
            _active,
            _active.Time);

        ActionStarted?.Invoke(
            action);
    }


    /// <summary>
    /// 按名称查找并淡入播放动作。
    /// </summary>
    public void CrossFade(
        string actionName,
        float fadeDuration = -1f,
        bool loop = false)
    {
        ActionData action =
            GetAction(
                actionName);

        if (action == null)
        {
            Debug.LogWarning(
                $"[ActionPlayer] 淡入失败：找不到名为 \"{actionName}\" 的 ActionData。");

            return;
        }

        CrossFade(
            action,
            fadeDuration,
            loop);
    }


    /// <summary>
    /// 停止播放并清理所有运行时状态。
    /// </summary>
    public void Stop()
    {
        if (_fadingOut != null)
        {
            TeardownPlayback(
                _fadingOut,
                true);

            _fadingOut =
                null;
        }

        if (_active != null)
        {
            TeardownPlayback(
                _active,
                true);

            _active =
                null;
        }

        ClearSlots();

        if (_audioSource != null)
        {
            _audioSource.Stop();
        }

        CurrentAction =
            null;

        IsPlaying =
            false;

        _paused =
            false;

        _fadeTimer =
            0f;
    }


    /// <summary>
    /// 暂停播放，不清理已经创建的特效和音效状态。
    /// </summary>
    public void Pause()
    {
        if (!IsPlaying ||
            _paused)
        {
            return;
        }

        _paused =
            true;

        if (_graph.IsValid())
        {
            _graph.Stop();
        }
    }


    /// <summary>
    /// 恢复暂停的播放。
    /// </summary>
    public void Resume()
    {
        if (!_paused)
        {
            return;
        }

        _paused =
            false;

        ResumeGraph();
    }


    // =========================================================
    // 播放更新
    // =========================================================

    private void Tick(
        float deltaTime)
    {
        if (_active == null)
        {
            return;
        }


        // 已结束的动作只需要保持最终的特效状态。
        if (_active.Finished)
        {
            UpdateEffects(
                _active);

            return;
        }

        if (!IsPlaying ||
            _paused)
        {
            return;
        }

        _active.Time +=
            deltaTime;


        // 更新当前播放状态。
        if (_fadingOut != null)
        {
            _fadeTimer +=
                deltaTime;

            float k =
                Mathf.Clamp01(
                    _fadeTimer /
                    _fadeDuration);

            SetSlotWeights(
                1f - k,
                k);

            ReplaceSlotClip(
                1,
                GetAnimationClipAt(
                    _active,
                    _active.Time));

            if (k >= 1f)
            {
                FinishFade();
            }
        }
        else
        {
            ReplaceSlotClip(
                0,
                GetAnimationClipAt(
                    _active,
                    _active.Time));
        }


        // 循环回到开头时重置所有事件状态。
        if (IsLooping &&
            _active.Duration > 0f &&
            _active.Time >= _active.Duration)
        {
            _active.Time =
                Mathf.Repeat(
                    _active.Time,
                    _active.Duration);

            ResetPlaybackForLoop(
                _active);

            int slot =
                _fadingOut != null
                    ? 1
                    : 0;

            ReplaceSlotClip(
                slot,
                GetAnimationClipAt(
                    _active,
                    _active.Time));
        }


        // 更新音效、特效、碰撞盒和事件。
        Evaluate(
            _active,
            _active.Time);

        UpdateEffects(
            _active);


        // 结束非循环动作的播放。
        if (!IsLooping &&
            _active.Duration > 0f &&
            _active.Time >= _active.Duration)
        {
            CompleteAction();
        }
    }


    /// <summary>
    /// 更新 CrossFade 的混合权重。
    /// </summary>
    private void FinishFade()
    {
        if (_fadingOut != null)
        {
            // 淡入期间继续更新正在淡出的动作。
            _active.Effects.AddRange(
                _fadingOut.Effects);

            _fadingOut.Effects.Clear();

            _fadingOut =
                null;
        }

        MoveSlot1ToSlot0();

        _fadeTimer =
            0f;
    }


    /// <summary>
    /// 完成非循环动作的自然结束流程。
    /// </summary>
    private void CompleteAction()
    {
        if (_active == null)
        {
            return;
        }

        EndStateEvents(
            _active);

        DestroyHitboxes(
            _active);


        // 特效的结束由 UpdateEffects 统一处理。
        _active.Finished =
            true;

        IsPlaying =
            false;

        ActionData completed =
            CurrentAction;

        ActionCompleted?.Invoke(
            completed);
    }


    // =========================================================
    // 初始化 PlayableGraph
    // =========================================================

    private void EnsureGraph()
    {
        if (_graphCreated)
        {
            return;
        }

        GameObject target =
            MasterObject;

        _animator =
            target.GetComponent<Animator>();

        if (_animator == null)
        {
            _animator =
                target.AddComponent<Animator>();
        }

        _audioSource =
            target.GetComponent<AudioSource>();

        if (_audioSource == null)
        {
            _audioSource =
                target.AddComponent<AudioSource>();
        }

        _graph =
            PlayableGraph.Create(
                "ActionPlayer");

        _graph.SetTimeUpdateMode(
            DirectorUpdateMode.GameTime);

        _mixer =
            AnimationMixerPlayable.Create(
                _graph,
                2);

        AnimationPlayableOutput output =
            AnimationPlayableOutput.Create(
                _graph,
                "ActionPlayer",
                _animator);

        output.SetSourcePlayable(
            _mixer);

        _graph.Play();

        _graphCreated =
            true;
    }


    private void ShutdownGraph()
    {
        if (_active != null)
        {
            TeardownPlayback(
                _active,
                true);

            _active =
                null;
        }

        if (_fadingOut != null)
        {
            TeardownPlayback(
                _fadingOut,
                true);

            _fadingOut =
                null;
        }

        if (_graphCreated &&
            _graph.IsValid())
        {
            _graph.Destroy();
        }

        _graphCreated =
            false;
    }


    private void ResumeGraph()
    {
        if (_graphCreated &&
            _graph.IsValid() &&
            !_paused)
        {
            _graph.Play();
        }
    }


    // =========================================================
    // 动画槽位管理
    // =========================================================

    private void SetSlotWeights(
        float slot0,
        float slot1)
    {
        _slot0Weight =
            slot0;

        _slot1Weight =
            slot1;

        ApplySlotWeights();
    }


    private void ApplySlotWeights()
    {
        if (!_graphCreated)
        {
            return;
        }

        _mixer.SetInputWeight(
            0,
            _slot0Weight);

        _mixer.SetInputWeight(
            1,
            _slot1Weight);
    }


    private void DestroySlot(
        int slot)
    {
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


    /// <summary>
    /// 替换指定槽位中的动画片段。
    /// </summary>
    private void ReplaceSlotClip(
        int slot,
        AnimationClip clip,
        float initialLocalTime = -1f)
    {
        AnimationClip current =
            slot == 0
                ? _slot0Clip
                : _slot1Clip;

        if (current == clip)
        {
            return;
        }

        DestroySlot(
            slot);

        if (clip != null)
        {
            AnimationClipPlayable clipPlayable =
                AnimationClipPlayable.Create(
                    _graph,
                    clip);

            // Transition StartPercent：
            // 片段首次放入槽位时定位到指定的本地时间
            if (initialLocalTime > 0f)
            {
                clipPlayable.SetTime(
                    initialLocalTime);
            }

            _graph.Connect(
                clipPlayable,
                0,
                _mixer,
                slot);
        }

        if (slot == 0)
        {
            _slot0Clip =
                clip;
        }
        else
        {
            _slot1Clip =
                clip;
        }

        ApplySlotWeights();
    }


    /// <summary>
    /// CrossFade 完成后，将槽位 1 的 playable 移到槽位 0。
    /// </summary>
    private void MoveSlot1ToSlot0()
    {
        Playable incoming =
            _mixer.GetInput(
                1);

        DestroySlot(
            0);

        if (incoming.IsValid())
        {
            _graph.Disconnect(
                _mixer,
                1);

            _graph.Connect(
                incoming,
                0,
                _mixer,
                0);
        }

        _slot0Clip =
            _slot1Clip;

        _slot1Clip =
            null;

        SetSlotWeights(
            1f,
            0f);
    }


    private void ClearSlots()
    {
        DestroySlot(
            0);

        DestroySlot(
            1);

        _slot0Clip =
            null;

        _slot1Clip =
            null;

        SetSlotWeights(
            1f,
            0f);
    }


    // =========================================================
    // Playback 数据和评估
    // =========================================================

    private static ActionPlayback CreatePlayback(
        ActionData data,
        bool loop)
    {
        ActionPlayback playback =
            new ActionPlayback();

        playback.Data =
            data;

        playback.Loop =
            loop;

        playback.Time =
            0f;

        playback.LastEventTime =
            -1f;

        playback.Duration =
            CalculateDuration(
                data);

        return playback;
    }


    /// <summary>
    /// 计算动作结束时间。
    /// 优先使用 Clip 的 EndTime 作为事件判断依据。
    /// </summary>
    private static float CalculateDuration(
        ActionData action)
    {
        if (action == null ||
            action.Tracks == null)
        {
            return 0f;
        }

        float duration =
            0f;

        foreach (
            TrackData track
            in action.Tracks)
        {
            if (track == null)
            {
                continue;
            }

            if (track.Clips != null)
            {
                foreach (
                    BaseClipData clip
                    in track.Clips)
                {
                    if (clip == null)
                    {
                        continue;
                    }

                    if (clip.EndTime > duration)
                    {
                        duration =
                            clip.EndTime;
                    }
                }
            }

            if (track.PointEvents != null)
            {
                foreach (
                    PointEventData pointEvent
                    in track.PointEvents)
                {
                    if (pointEvent == null)
                    {
                        continue;
                    }

                    if (pointEvent.Time > duration)
                    {
                        duration =
                            pointEvent.Time;
                    }
                }
            }
        }

        return duration;
    }


    /// <summary>
    /// CrossFade 开始时转移淡出动作的事件状态。
    /// 淡出动作不会再次触发 OnEnd。
    /// </summary>
    private void FadeOutLogic(
        ActionPlayback playback)
    {
        EndStateEvents(
            playback);

        DestroyHitboxes(
            playback);
    }


    /// <summary>
    /// 停止一个播放实例，并按需清理其特效状态。
    /// </summary>
    private void TeardownPlayback(
        ActionPlayback playback,
        bool destroyEffects)
    {
        if (playback == null)
        {
            return;
        }

        EndStateEvents(
            playback);

        DestroyHitboxes(
            playback);

        if (destroyEffects)
        {
            DestroyEffects(
                playback);
        }
    }


    /// <summary>
    /// 循环动作回到开头时重置事件状态。
    /// </summary>
    private void ResetPlaybackForLoop(
        ActionPlayback playback)
    {
        EndStateEvents(
            playback);

        DestroyHitboxes(
            playback);

        DestroyEffects(
            playback);

        playback.TriggeredVoices.Clear();

        playback.TriggeredEffects.Clear();

        playback.LastEventTime =
            -1f;
    }


    // =========================================================
    // 每帧评估
    // =========================================================

    private void Evaluate(
        ActionPlayback playback,
        float time)
    {
        if (playback.Data == null ||
            playback.Data.Tracks == null)
        {
            return;
        }

        float last =
            playback.LastEventTime;

        // 即使本帧 delta 为 0，也保证 OnUpdate 至少触发一次。
        float delta =
            last < 0f
                ? 0f
                : time - last;

        foreach (
            TrackData track
            in playback.Data.Tracks)
        {
            if (track == null)
            {
                continue;
            }

            if (track.Clips != null)
            {
                foreach (
                    BaseClipData clip
                    in track.Clips)
                {
                    if (clip == null)
                    {
                        continue;
                    }

                    // 继续评估 PlayableGraph。
                    if (clip is AnimationClipData)
                    {
                        continue;
                    }

                    if (clip is VoiceClipData voice)
                    {
                        if (voice.Voice == null)
                        {
                            continue;
                        }

                        bool crossed =
                            last < voice.StartTime &&
                            time >= voice.StartTime;

                        if (crossed &&
                            !playback.TriggeredVoices.Contains(
                                voice))
                        {
                            playback.TriggeredVoices.Add(
                                voice);

                            PlayVoice(
                                voice.Voice);
                        }

                        continue;
                    }

                    if (clip is EffectClipData effect)
                    {
                        bool crossed =
                            last < effect.StartTime &&
                            time >= effect.StartTime;

                        if (crossed &&
                            !playback.TriggeredEffects.Contains(
                                effect))
                        {
                            playback.TriggeredEffects.Add(
                                effect);

                            SpawnEffect(
                                playback,
                                effect);
                        }

                        continue;
                    }

                    // BeHitboxClipData 继承自 HitboxClipData。
                    if (clip is HitboxClipData hitbox)
                    {
                        UpdateHitbox(
                            playback,
                            hitbox,
                            time);

                        continue;
                    }

                    if (clip is StateEventData stateEvent)
                    {
                        UpdateStateEvent(
                            playback,
                            stateEvent,
                            time,
                            delta);

                        continue;
                    }
                }
            }

            if (track.PointEvents != null)
            {
                foreach (
                    PointEventData pointEvent
                    in track.PointEvents)
                {
                    if (pointEvent == null)
                    {
                        continue;
                    }

                    CheckPointEvent(
                        playback,
                        pointEvent);
                }
            }
        }

        playback.LastEventTime =
            time;
    }


    // =========================================================
    // 动画片段
    // =========================================================

    /// <summary>
    /// 获取指定时间点正在使用的动画片段。
    /// 没有匹配项时返回列表中的第一个动画片段。
    /// </summary>
    private static AnimationClip GetAnimationClipAt(
        ActionPlayback playback,
        float time)
    {
        if (playback.Data == null ||
            playback.Data.Tracks == null)
        {
            return null;
        }

        AnimationClip fallback =
            null;

        foreach (
            TrackData track
            in playback.Data.Tracks)
        {
            if (track == null ||
                track.Clips == null)
            {
                continue;
            }

            foreach (
                BaseClipData clip
                in track.Clips)
            {
                AnimationClipData animationClip =
                    clip as AnimationClipData;

                if (animationClip == null ||
                    animationClip.Animation == null)
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback =
                        animationClip.Animation;
                }

                if (animationClip.ContainsTime(
                        time))
                {
                    return animationClip.Animation;
                }
            }
        }

        return fallback;
    }


    /// <summary>
    /// 按 StartPercent（0~1）设置播放起始时间。
    ///
    /// 同时把 LastEventTime 定位到起始时刻，
    /// 保证起始位置之前的音效 / 特效 / 点事件不会被补触发。
    /// </summary>
    private static void ApplyStartTime(
        ActionPlayback playback,
        float startPercent)
    {
        if (playback == null ||
            playback.Duration <= 0f)
        {
            return;
        }

        float percent =
            Mathf.Clamp01(
                startPercent);

        if (percent <= 0f)
        {
            return;
        }

        float startTime =
            percent *
            playback.Duration;

        playback.Time =
            startTime;

        playback.LastEventTime =
            startTime;
    }


    /// <summary>
    /// 获取指定时刻所在动画 Clip 内对应的本地播放时间。
    /// 用于 Transition StartPercent 定位动画片段。
    /// </summary>
    private static float GetAnimationClipLocalTime(
        ActionPlayback playback,
        float time)
    {
        if (playback.Data == null ||
            playback.Data.Tracks == null)
        {
            return 0f;
        }

        foreach (TrackData track in playback.Data.Tracks)
        {
            if (track == null ||
                track.Clips == null)
            {
                continue;
            }

            foreach (BaseClipData clip in track.Clips)
            {
                if (!(clip is AnimationClipData animationClip) ||
                    animationClip.Animation == null)
                {
                    continue;
                }

                if (animationClip.ContainsTime(time))
                {
                    float localTime =
                        time -
                        animationClip.StartTime;

                    return Mathf.Clamp(
                        localTime,
                        0f,
                        animationClip.Animation.length);
                }
            }
        }

        return 0f;
    }


    // =========================================================
    // 音效
    // =========================================================

    private void PlayVoice(
        AudioClip voice)
    {
        if (_audioSource == null ||
            voice == null)
        {
            return;
        }

        _audioSource.PlayOneShot(
            voice);
    }


    // =========================================================
    // 特效
    // =========================================================

    private void SpawnEffect(
        ActionPlayback playback,
        EffectClipData clip)
    {
        if (clip.EffectPrefab == null)
        {
            return;
        }

        GameObject instance =
            Instantiate(
                clip.EffectPrefab);

        AttachToBone(
            instance.transform,
            clip.AttachBone,
            clip.LocalOffset);

        playback.Effects.Add(
            new EffectRuntime
            {
                GameObject =
                    instance,

                DestroyAtGameTime =
                    Time.time +
                    clip.Length +
                    0.5f,
            });
    }


    private void UpdateEffects(
        ActionPlayback playback)
    {
        for (
            int i = playback.Effects.Count - 1;
            i >= 0;
            i--)
        {
            EffectRuntime fx =
                playback.Effects[i];

            if (fx.GameObject == null)
            {
                playback.Effects.RemoveAt(
                    i);

                continue;
            }

            if (Time.time >=
                fx.DestroyAtGameTime)
            {
                Destroy(
                    fx.GameObject);

                playback.Effects.RemoveAt(
                    i);
            }
        }
    }


    private static void DestroyEffects(
        ActionPlayback playback)
    {
        foreach (
            EffectRuntime fx
            in playback.Effects)
        {
            if (fx.GameObject != null)
            {
                UnityEngine.Object.Destroy(
                    fx.GameObject);
            }
        }

        playback.Effects.Clear();
    }


    // =========================================================
    // Hitbox / Behitbox
    //
    // 播放期间 Collider 始终保持 enabled。
    // 实际碰撞状态只由 Hitbox.activate 控制。
    // =========================================================

    private void UpdateHitbox(
        ActionPlayback playback,
        HitboxClipData clip,
        float time)
    {
        if (clip.HitboxPrefab == null)
        {
            return;
        }

        GameObject instance =
            null;

        playback.HitboxInstances.TryGetValue(
            clip.HitboxPrefab,
            out instance);

        bool inWindow =
            clip.ContainsTime(
                time);

        if (!inWindow)
        {
            if (instance != null)
            {
                Hitbox inactive =
                    instance.GetComponent<Hitbox>();

                if (inactive != null)
                {
                    inactive.activate =
                        false;
                }
            }

            return;
        }

        if (instance == null)
        {
            instance =
                Instantiate(
                    clip.HitboxPrefab);

            playback.HitboxInstances[
                clip.HitboxPrefab] =
                instance;


            // 播放期间保持 Collider 启用。
            Collider collider =
                instance.GetComponent<Collider>();

            if (collider != null)
            {
                collider.enabled =
                    true;
            }

            AttachToBone(
                instance.transform,
                clip.AttachBone,
                clip.LocalOffset);
        }

        Hitbox hitbox =
            instance.GetComponent<Hitbox>();

        if (hitbox != null)
        {
            hitbox.activate =
                true;
        }
    }


    private static void DestroyHitboxes(
        ActionPlayback playback)
    {
        foreach (
            var pair
            in playback.HitboxInstances)
        {
            if (pair.Value != null)
            {
                UnityEngine.Object.Destroy(
                    pair.Value);
            }
        }

        playback.HitboxInstances.Clear();
    }


    // =========================================================
    // 时间线事件
    // =========================================================

    private void UpdateStateEvent(
        ActionPlayback playback,
        StateEventData stateEvent,
        float time,
        float deltaTime)
    {
        if (stateEvent.StateEvent == null)
        {
            return;
        }

        bool inRange =
            stateEvent.ContainsTime(
                time);

        bool active =
            playback.ActiveStateEvents.Contains(
                stateEvent);

        if (inRange)
        {
            if (!active)
            {
                stateEvent
                    .StateEvent
                    .OnStart();

                playback.ActiveStateEvents.Add(
                    stateEvent);
            }

            stateEvent
                .StateEvent
                .OnUpdate(
                    deltaTime);
        }
        else
        {
            if (active)
            {
                stateEvent
                    .StateEvent
                    .OnEnd();

                playback.ActiveStateEvents.Remove(
                    stateEvent);
            }
        }
    }


    private static void EndStateEvents(
        ActionPlayback playback)
    {
        foreach (
            StateEventData stateEvent
            in playback.ActiveStateEvents)
        {
            if (stateEvent != null &&
                stateEvent.StateEvent != null)
            {
                stateEvent
                    .StateEvent
                    .OnEnd();
            }
        }

        playback.ActiveStateEvents.Clear();
    }


    // =========================================================
    // 调用事件
    // =========================================================

    private void CheckPointEvent(
        ActionPlayback playback,
        PointEventData pointEvent)
    {
        if (pointEvent.PointEvent == null)
        {
            return;
        }

        float pointTime =
            pointEvent.Time;

        float last =
            playback.LastEventTime;

        // 当 lastTime < pointTime <= currentTime 时，表示越过了事件点。
        if (last < pointTime &&
            playback.Time >= pointTime)
        {
            pointEvent
                .PointEvent
                .OnCall();
        }
    }


    // =========================================================
    // 辅助方法
    // =========================================================

    private void AttachToBone(
        Transform instanceTransform,
        string attachBone,
        Vector3 localOffset)
    {
        Transform root =
            MasterObject.transform;

        Transform target =
            root;

        if (!string.IsNullOrEmpty(
                attachBone))
        {
            Transform bone =
                DeepFind(
                    root,
                    attachBone);

            if (bone != null)
            {
                target =
                    bone;
            }
            else
            {
                Debug.LogWarning(
                    $"[ActionPlayer] 找不到特效所需的挂点 \"{attachBone}\"，请检查角色层级。");
            }
        }

        instanceTransform.SetParent(
            target,
            false);

        instanceTransform.localPosition =
            localOffset;
    }


    /// <summary>
    /// 按名称递归查找子 Transform。
    /// </summary>
    private static Transform DeepFind(
        Transform root,
        string name)
    {
        if (root == null ||
            string.IsNullOrEmpty(name))
        {
            return null;
        }

        if (root.name == name)
        {
            return root;
        }

        foreach (
            Transform child
            in root)
        {
            Transform found =
                DeepFind(
                    child,
                    name);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }


    // =========================================================
    // 特效运行时状态
    // =========================================================

    private class ActionPlayback
    {
        public ActionData Data;

        public bool Loop;

        public float Time;

        public float Duration;

        public float LastEventTime =
            -1f;

        public bool Finished;


        // 已创建的特效实例。
        public readonly HashSet<VoiceClipData>
            TriggeredVoices =
                new HashSet<VoiceClipData>();

        // 成功创建的特效实例。
        public readonly HashSet<EffectClipData>
            TriggeredEffects =
                new HashSet<EffectClipData>();

        // 已触发 OnStart 但尚未触发 OnEnd 的持续事件。
        public readonly HashSet<StateEventData>
            ActiveStateEvents =
                new HashSet<StateEventData>();

        // 特效是否在 CrossFade 前的动作中创建。
        public readonly List<EffectRuntime>
            Effects =
                new List<EffectRuntime>();

        // prefab 对应的实例。
        public readonly Dictionary<
            GameObject,
            GameObject> HitboxInstances =
                new Dictionary<
                    GameObject,
                    GameObject>();
    }


    private class EffectRuntime
    {
        public GameObject GameObject;

        public float DestroyAtGameTime;
    }
}