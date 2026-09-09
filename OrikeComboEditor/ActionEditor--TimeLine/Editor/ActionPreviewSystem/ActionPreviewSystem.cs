using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class ActionPreviewSystem
{
    private GameObject _character;

    private ActionData _actionData;

    private AudioClip _currentVoice;

    private int _currentVoiceSample = -1;

    private float _lastActionTime = -1f;

    private float _voiceStopTime = -1f;

    private bool _voicePlaying;


    // =========================================================
    // Effect
    // =========================================================

    private GameObject _currentEffectInstance;

    private GameObject _currentEffectPrefab;


    // =========================================================
    // Hitbox / Behitbox
    //
    // 多个 hitbox 可能同时激活
    // 用 prefab 作为 key 缓存实例
    // 每帧先全部重置为 activate=false
    // 再按当前激活的 Clip 设为 true
    // =========================================================

    private readonly Dictionary<
        GameObject,
        GameObject> _hitboxInstances =
        new Dictionary<
            GameObject,
            GameObject>();


    // =========================================================
    // State Events
    //
    // 当前已触发 OnStart 但未 OnEnd 的持续事件
    // 进入 Clip 时间段时加入，离开时移除并调用 OnEnd
    // =========================================================

    private readonly HashSet<
        StateEventData> _activeStateEvents =
        new HashSet<
            StateEventData>();


    // =========================================================
    // Constructor
    // =========================================================

    public ActionPreviewSystem(
        GameObject character,
        ActionData actionData)
    {
        _character =
            character;

        _actionData =
            actionData;

        // 主动注册 Scene 视图绘制回调
        //
        // hitbox 预览实例使用 HideFlags.HideAndDontSave
        // 不在 Hierarchy 中，Unity 的 [DrawGizmo] 自动发现
        // 机制不会为这类对象调用绘制
        // 所以这里用 duringSceneGui 主动遍历绘制
        SceneView.duringSceneGui +=
            OnSceneGUI;
    }


    // =========================================================
    // Dispose
    //
    // 窗口关闭时调用，反注册 Scene 回调
    // =========================================================

    public void Dispose()
    {
        SceneView.duringSceneGui -=
            OnSceneGUI;

        StopHitboxIfNeeded();

        StopEffectIfNeeded();
    }


    // =========================================================
    // Set Character
    // =========================================================

    public void SetCharacter(
        GameObject character)
    {
        _character =
            character;
    }


    // =========================================================
    // Set Action Data
    // =========================================================

    public void SetActionData(
        ActionData actionData)
    {
        if (_actionData != actionData)
        {
            StopPreview();

            StopEffectIfNeeded();

            StopHitboxIfNeeded();
        }

        _actionData =
            actionData;
    }


    // =========================================================
    // Preview
    //
    // frameRate:
    //     ?????? TimeLineWindow ??? FPS
    //
    // isPlaying:
    //     false = ??????
    //     true  = ????????
    // =========================================================

    public void Preview(
        float actionTime,
        float frameRate,
        bool isPlaying)
    {
        if (frameRate <= 0f)
        {
            frameRate = 60f;
        }

        if (_actionData == null)
        {
            StopVoiceIfNeeded();

            StopEffectIfNeeded();

            StopHitboxIfNeeded();

            StopStateEvents();

            return;
        }

        if (_actionData.Tracks == null)
        {
            StopVoiceIfNeeded();

            StopEffectIfNeeded();

            StopHitboxIfNeeded();

            StopStateEvents();

            return;
        }

        // =================================================
        // Hitbox 每帧重置
        // 把所有缓存实例的 activate / collider.enabled 关掉
        // 后面 PreviewTrack 会按需重新激活
        // =================================================

        ResetHitboxActivation();

        bool hasVoice = false;

        bool hasEffect = false;

        bool hasHitbox = false;

        foreach (
            TrackData track
            in _actionData.Tracks)
        {
            if (track == null)
            {
                continue;
            }

            PreviewTrack(
                track,
                actionTime,
                frameRate,
                isPlaying,
                ref hasVoice,
                ref hasEffect,
                ref hasHitbox);
        }

        if (!hasVoice)
        {
            StopVoiceIfNeeded();
        }

        if (!hasEffect)
        {
            StopEffectIfNeeded();
        }

        if (!hasHitbox)
        {
            StopHitboxIfNeeded();
        }

        _lastActionTime =
            actionTime;

        // 确保 Scene 视图重绘
        // 让 hitbox gizmo 及时更新（包括 inactive 状态）
        SceneView.RepaintAll();
    }


    // =========================================================
    // Preview Track
    // =========================================================

    private void PreviewTrack(
        TrackData track,
        float actionTime,
        float frameRate,
        bool isPlaying,
        ref bool hasVoice,
        ref bool hasEffect,
        ref bool hasHitbox)
    {
        if (track == null)
        {
            return;
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

                // =================================================
                // State Event 持续事件
                // 不走 PreviewData，直接调用事件方法
                // =================================================

                if (clip is StateEventData
                    stateEvent)
                {
                    UpdateStateEvent(
                        stateEvent,
                        actionTime);

                    continue;
                }


                BasePreviewData previewData =
                    clip.GetPreviewDataAtTime(
                        actionTime);

                if (previewData == null)
                {
                    continue;
                }

                if (previewData
                    is VoicePreviewData)
                {
                    hasVoice = true;
                }

                if (previewData
                    is EffectPreviewData)
                {
                    hasEffect = true;
                }

                if (previewData
                    is HitboxPreviewData)
                {
                    hasHitbox = true;
                }

                ApplyPreviewData(
                    previewData,
                    frameRate,
                    isPlaying);
            }
        }


        // =====================================================
        // Point Event 点事件
        // 检测 Playhead 是否穿越事件时间点
        // =====================================================

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
                    pointEvent,
                    actionTime);
            }
        }
    }


    // =========================================================
    // Update State Event
    //
    // 根据当前时间决定调用 OnStart / OnUpdate / OnEnd
    // =========================================================

    private void UpdateStateEvent(
        StateEventData stateEvent,
        float actionTime)
    {
        if (stateEvent == null ||
            stateEvent.StateEvent == null)
        {
            return;
        }

        bool inRange =
            stateEvent.ContainsTime(
                actionTime);

        bool active =
            _activeStateEvents.Contains(
                stateEvent);

        float delta =
            actionTime -
            _lastActionTime;

        if (inRange)
        {
            if (!active)
            {
                // 进入时间段
                stateEvent
                    .StateEvent
                    .OnStart();

                _activeStateEvents.Add(
                    stateEvent);
            }

            // 时间段内每帧更新
            stateEvent
                .StateEvent
                .OnUpdate(delta);
        }
        else
        {
            if (active)
            {
                // 离开时间段
                stateEvent
                    .StateEvent
                    .OnEnd();

                _activeStateEvents.Remove(
                    stateEvent);
            }
        }
    }


    // =========================================================
    // Check Point Event
    //
    // 当 Playhead 穿越事件时间点时调用 OnCall
    // 支持正向和反向拖动
    // =========================================================

    private void CheckPointEvent(
        PointEventData pointEvent,
        float actionTime)
    {
        if (pointEvent == null ||
            pointEvent.PointEvent == null)
        {
            return;
        }

        float pointTime =
            pointEvent.Time;

        float lastTime =
            _lastActionTime;

        // 首次调用不触发
        if (lastTime < 0f)
        {
            return;
        }

        bool crossed = false;

        // 正向：lastTime < pointTime <= actionTime
        if (lastTime < pointTime &&
            actionTime >= pointTime)
        {
            crossed = true;
        }

        // 反向：actionTime <= pointTime < lastTime
        if (actionTime < pointTime &&
            lastTime >= pointTime)
        {
            crossed = true;
        }

        if (crossed)
        {
            pointEvent
                .PointEvent
                .OnCall();
        }
    }


    // =========================================================
    // Apply Preview Data
    // =========================================================

    private void ApplyPreviewData(
        BasePreviewData previewData,
        float frameRate,
        bool isPlaying)
    {
        if (previewData == null)
        {
            return;
        }

        if (previewData
            is AnimationPreviewData)
        {
            AnimationPreviewData animationData =
                previewData
                as AnimationPreviewData;

            ApplyAnimationPreview(
                animationData);

            return;
        }

        if (previewData
            is VoicePreviewData)
        {
            VoicePreviewData voiceData =
                previewData
                as VoicePreviewData;

            ApplyVoicePreview(
                voiceData,
                frameRate,
                isPlaying);

            return;
        }

        if (previewData
            is EffectPreviewData)
        {
            EffectPreviewData effectData =
                previewData
                as EffectPreviewData;

            ApplyEffectPreview(
                effectData);

            return;
        }

        if (previewData
            is HitboxPreviewData)
        {
            HitboxPreviewData hitboxData =
                previewData
                as HitboxPreviewData;

            ApplyHitboxPreview(
                hitboxData);

            return;
        }
    }


    // =========================================================
    // Animation Preview
    // =========================================================

    private void ApplyAnimationPreview(
        AnimationPreviewData previewData)
    {
        if (_character == null)
        {
            return;
        }

        if (previewData == null)
        {
            return;
        }

        if (previewData.Animation == null)
        {
            return;
        }

        previewData.Animation.SampleAnimation(
            _character,
            previewData.LocalTime);

        SceneView.RepaintAll();
    }


    // =========================================================
    // Voice Preview
    // =========================================================

    private void ApplyVoicePreview(
        VoicePreviewData previewData,
        float frameRate,
        bool isPlaying)
    {
        if (previewData == null ||
            previewData.Voice == null)
        {
            return;
        }

        AudioClip voice =
            previewData.Voice;

        float localTime =
            Mathf.Max(
                0f,
                previewData.LocalTime);

        int sample =
            Mathf.RoundToInt(
                localTime *
                voice.frequency);

        sample =
            Mathf.Clamp(
                sample,
                0,
                Mathf.Max(
                    0,
                    voice.samples - 1));


        // =====================================================
        // ????????
        //
        // ???? Voice ???????????
        // AudioUtil ??????????
        // =====================================================

        if (isPlaying)
        {
            if (_currentVoice == voice)
            {
                return;
            }

            StopVoiceIfNeeded();

            _currentVoice =
                voice;

            _currentVoiceSample =
                sample;

            _voicePlaying =
                true;

            _voiceStopTime =
                -1f;

            EditorAudioPreview.Play(
                voice,
                sample);

            return;
        }


        // =====================================================
        // ???????
        //
        // ?????????????????
        // ????????????
        // =====================================================

        if (_currentVoice == voice &&
            _currentVoiceSample == sample &&
            _voicePlaying)
        {
            return;
        }

        StopVoiceIfNeeded();

        _currentVoice =
            voice;

        _currentVoiceSample =
            sample;


        float frameDuration =
            1f /
            Mathf.Max(
                1f,
                frameRate);


        _voiceStopTime =
            (float)EditorApplication.timeSinceStartup +
            frameDuration;

        _voicePlaying =
            true;


        EditorAudioPreview.Play(
            voice,
            sample);
    }


    // =========================================================
    // Effect Preview
    //
    // 实例化预制体到角色位置
    // 用 ParticleSystem.Simulate 按时间采样到指定时间点
    // =========================================================

    private void ApplyEffectPreview(
        EffectPreviewData previewData)
    {
        if (previewData == null ||
            previewData.EffectPrefab == null)
        {
            StopEffectIfNeeded();

            return;
        }

        // =====================================================
        // 切换预制体
        // 销毁旧实例，重新实例化
        // =====================================================

        if (_currentEffectPrefab !=
            previewData.EffectPrefab)
        {
            StopEffectIfNeeded();

            _currentEffectPrefab =
                previewData.EffectPrefab;

            _currentEffectInstance =
                Object.Instantiate(
                    previewData.EffectPrefab);

            _currentEffectInstance.hideFlags =
                HideFlags.HideAndDontSave;

            _currentEffectInstance
                .SetActive(
                    true);

            // 编辑模式下实例化的 ParticleSystem
            // 必须先 Play 进入 playing 状态
            // ParticleSystemRenderer 才会填充 / 渲染粒子
            //
            // Play(true) 带 children，统一播放所有子粒子
            // 编辑模式不会自动推进时间，进度由 Simulate 控制
            ParticleSystem rootPS =
                _currentEffectInstance
                    .GetComponentInChildren<
                        ParticleSystem>(true);

            if (rootPS != null)
            {
                rootPS.Play(
                    true);
            }
        }

        // =====================================================
        // 没有实例
        // 预览数据可能在中途丢失实例
        // =====================================================

        if (_currentEffectInstance == null)
        {
            return;
        }

        // =====================================================
        // 挂载到目标骨骼
        //
        // AttachBone 为空 = 角色根
        // 否则递归查找角色 Transform 树中第一个同名节点
        //
        // SetParent(target, false)
        //   保留 local 变换，跟随父节点一起移动 / 旋转
        //   适合武器 / 剑光 / 手部特效
        //
        // localPosition = LocalOffset
        //   在挂载点基础上叠加局部偏移
        // =====================================================

        Transform attachTarget = null;

        if (_character != null)
        {
            attachTarget =
                _character
                    .transform;

            if (!string.IsNullOrEmpty(
                    previewData.AttachBone))
            {
                Transform bone =
                    DeepFind(
                        _character
                            .transform,
                        previewData
                            .AttachBone);

                if (bone != null)
                {
                    attachTarget =
                        bone;
                }
            }
        }

        if (attachTarget != null)
        {
            _currentEffectInstance
                .transform
                .SetParent(
                    attachTarget,
                    false);

            _currentEffectInstance
                .transform
                .localPosition =
                previewData
                    .LocalOffset;
        }
        else
        {
            // 没有角色
            // 放到世界原点
            _currentEffectInstance
                .transform
                .SetParent(
                    null,
                    false);

            _currentEffectInstance
                .transform
                .position =
                Vector3.zero;
        }

        // =====================================================
        // 用 Simulate 跳到指定时间点
        //
        // 在根 ParticleSystem 上调用
        // withChildren = true
        //   由 Unity 内部统一模拟所有子粒子
        //   正确处理父子粒子的 startDelay / 嵌套关系
        //
        // restart = true
        //   从 0 重新模拟到 localTime
        //   适合拖动 Playhead 随机跳转
        // =====================================================

        ParticleSystem rootParticle =
            _currentEffectInstance
                .GetComponentInChildren<
                    ParticleSystem>(true);

        if (rootParticle != null)
        {
            rootParticle.Simulate(
                previewData.LocalTime,
                true,
                true);
        }

        SceneView.RepaintAll();
    }


    // =========================================================
    // Stop Effect
    // =========================================================

    private void StopEffectIfNeeded()
    {
        if (_currentEffectInstance == null &&
            _currentEffectPrefab == null)
        {
            return;
        }

        if (_currentEffectInstance != null)
        {
            Object.DestroyImmediate(
                _currentEffectInstance);

            _currentEffectInstance =
                null;
        }

        _currentEffectPrefab =
            null;
    }


    // =========================================================
    // Update
    // =========================================================

    public void Update()
    {
        if (!_voicePlaying)
        {
            return;
        }

        if (_voiceStopTime < 0f)
        {
            return;
        }

        double currentTime =
            EditorApplication.timeSinceStartup;

        if (currentTime >= _voiceStopTime)
        {
            StopVoiceIfNeeded();
        }
    }


    // =========================================================
    // Stop Voice
    // =========================================================

    private void StopVoiceIfNeeded()
    {
        if (_currentVoice == null &&
            !_voicePlaying)
        {
            return;
        }

        EditorAudioPreview.Stop();

        _currentVoice =
            null;

        _currentVoiceSample =
            -1;

        _voiceStopTime =
            -1f;

        _voicePlaying =
            false;
    }


    // =========================================================
    // Stop Preview
    // =========================================================

    public void StopPreview()
    {
        EditorAudioPreview.Stop();

        _currentVoice =
            null;

        _currentVoiceSample =
            -1;

        _lastActionTime =
            -1f;

        _voiceStopTime =
            -1f;

        _voicePlaying =
            false;

        if (_currentEffectInstance != null)
        {
            Object.DestroyImmediate(
                _currentEffectInstance);

            _currentEffectInstance =
                null;
        }

        _currentEffectPrefab =
            null;

        StopHitboxIfNeeded();

        StopStateEvents();
    }


    // =========================================================
    // Stop State Events
    //
    // 对所有活动中的持续事件调用 OnEnd
    // =========================================================

    private void StopStateEvents()
    {
        foreach (
            StateEventData stateEvent
            in _activeStateEvents)
        {
            if (stateEvent != null &&
                stateEvent.StateEvent != null)
            {
                stateEvent
                    .StateEvent
                    .OnEnd();
            }
        }

        _activeStateEvents.Clear();
    }


    // =========================================================
    // Reset Hitbox Activation
    //
    // 每帧开始调用
    // 把所有缓存实例的 activate / collider.enabled 关掉
    // 后面 ApplyHitboxPreview 会按需重新激活
    // =========================================================

    private void ResetHitboxActivation()
    {
        foreach (
            var pair
            in _hitboxInstances)
        {
            if (pair.Value == null)
            {
                continue;
            }

            Hitbox hitbox =
                pair.Value
                    .GetComponent<Hitbox>();

            if (hitbox != null)
            {
                hitbox.activate = false;
            }

            Collider collider =
                pair.Value
                    .GetComponent<Collider>();

            if (collider != null)
            {
                collider.enabled = false;
            }
        }
    }


    // =========================================================
    // Apply Hitbox Preview
    //
    // 在 Clip 时间段内：
    //   - 实例化 / 复用 hitbox 实例
    //   - 挂载到目标骨骼
    //   - activate = true
    //   - collider.enabled = true（可视化 + Scene Gizmo 显示）
    //
    // 编辑器预览用 collider.enabled 控制可视化
    // 游戏运行时 collider 应始终 enabled
    // 实际生效与否只看 Hitbox.activate
    // =========================================================

    private void ApplyHitboxPreview(
        HitboxPreviewData previewData)
    {
        if (previewData == null ||
            previewData.HitboxPrefab == null)
        {
            return;
        }

        // =================================================
        // 获取或创建实例
        // =================================================

        GameObject instance = null;

        _hitboxInstances
            .TryGetValue(
                previewData.HitboxPrefab,
                out instance);

        if (instance == null)
        {
            instance =
                Object.Instantiate(
                    previewData.HitboxPrefab);

            instance.hideFlags =
                HideFlags.HideAndDontSave;

            _hitboxInstances[
                previewData.HitboxPrefab] =
                instance;
        }

        // =================================================
        // 挂载到目标骨骼
        // =================================================

        Transform attachTarget = null;

        if (_character != null)
        {
            attachTarget =
                _character
                    .transform;

            if (!string.IsNullOrEmpty(
                    previewData.AttachBone))
            {
                Transform bone =
                    DeepFind(
                        _character
                            .transform,
                        previewData
                            .AttachBone);

                if (bone != null)
                {
                    attachTarget =
                        bone;
                }
            }
        }

        if (attachTarget != null)
        {
            instance
                .transform
                .SetParent(
                    attachTarget,
                    false);

            instance
                .transform
                .localPosition =
                previewData
                    .LocalOffset;
        }
        else
        {
            instance
                .transform
                .SetParent(
                    null,
                    false);

            instance
                .transform
                .position =
                Vector3.zero;
        }

        // =================================================
        // 激活
        // =================================================

        Hitbox hitbox =
            instance
                .GetComponent<Hitbox>();

        if (hitbox != null)
        {
            hitbox.activate = true;

            // 按类型设置 Gizmo 颜色
            // Hitbox 红色，Behitbox 紫色
            //
            // 优先看组件类型（prefab 上挂 Behitbox 脚本）
            // 其次看 Clip 数据类型
            bool isBehitbox =
                instance
                    .GetComponent<Behitbox>()
                != null ||
                previewData.ClipType ==
                ClipType.Behitbox;

            if (isBehitbox)
            {
                hitbox.gizmoColor =
                    new Color(
                        0.65f,
                        0.25f,
                        0.85f,
                        0.4f);
            }
            else
            {
                hitbox.gizmoColor =
                    new Color(
                        1f,
                        0.25f,
                        0.25f,
                        0.4f);
            }
        }

        Collider collider =
            instance
                .GetComponent<Collider>();

        if (collider != null)
        {
            collider.enabled = true;
        }

        SceneView.RepaintAll();
    }


    // =========================================================
    // Stop Hitbox
    //
    // 销毁所有缓存实例
    // =========================================================

    private void StopHitboxIfNeeded()
    {
        if (_hitboxInstances.Count == 0)
        {
            return;
        }

        foreach (
            var pair
            in _hitboxInstances)
        {
            if (pair.Value != null)
            {
                Object.DestroyImmediate(
                    pair.Value);
            }
        }

        _hitboxInstances.Clear();
    }


    // =========================================================
    // On Scene GUI
    //
    // 主动绘制所有缓存的 hitbox 实例
    //
    // 不依赖 Unity 的 [DrawGizmo] 自动发现
    // 因为预览实例使用 HideFlags.HideAndDontSave
    // 不在 Hierarchy 中，Gizmo 自动发现会跳过
    // =========================================================

    private void OnSceneGUI(
        SceneView sceneView)
    {
        if (_hitboxInstances.Count == 0)
        {
            return;
        }

        // 备份 Handles 状态
        Matrix4x4 oldMatrix =
            Handles.matrix;

        Color oldColor =
            Handles.color;

        foreach (
            var pair
            in _hitboxInstances)
        {
            GameObject instance =
                pair.Value;

            if (instance == null)
            {
                continue;
            }

            Hitbox hitbox =
                instance
                    .GetComponent<Hitbox>();

            Collider collider =
                instance
                    .GetComponent<Collider>();

            if (collider == null)
            {
                continue;
            }

            // =================================================
            // 颜色
            // 激活用 gizmoColor，未激活用灰色
            // =================================================

            if (hitbox != null &&
                hitbox.activate)
            {
                Handles.color =
                    hitbox.gizmoColor;
            }
            else
            {
                Handles.color =
                    new Color(
                        0.5f,
                        0.5f,
                        0.5f,
                        0.4f);
            }

            DrawColliderHandles(
                collider);
        }

        // 恢复 Handles 状态
        Handles.matrix =
            oldMatrix;

        Handles.color =
            oldColor;
    }


    // =========================================================
    // Draw Collider Handles
    // =========================================================

    private static void
        DrawColliderHandles(
            Collider collider)
    {
        if (collider is BoxCollider box)
        {
            Handles.matrix =
                box.transform
                    .localToWorldMatrix;

            Handles.DrawWireCube(
                box.center,
                box.size);

            return;
        }

        if (collider is SphereCollider sphere)
        {
            DrawSphereHandles(
                sphere.transform,
                sphere.center,
                sphere.radius);

            return;
        }

        if (collider is CapsuleCollider capsule)
        {
            DrawCapsuleHandles(
                capsule);

            return;
        }
    }


    // =========================================================
    // Draw Sphere Handles
    //
    // Handles 没有原生 wire sphere
    // 画 3 个正交圆盘模拟
    // =========================================================

    private static void
        DrawSphereHandles(
            Transform transform,
            Vector3 localCenter,
            float localRadius)
    {
        Vector3 worldCenter =
            transform.TransformPoint(
                localCenter);

        Vector3 lossyScale =
            transform.lossyScale;

        float radius =
            localRadius *
            Mathf.Max(
                Mathf.Abs(lossyScale.x),
                Mathf.Abs(lossyScale.y),
                Mathf.Abs(lossyScale.z));

        Quaternion rotation =
            transform.rotation;

        Handles.matrix =
            Matrix4x4.identity;

        // XY 平面
        Handles.DrawWireDisc(
            worldCenter,
            rotation * Vector3.forward,
            radius);

        // XZ 平面
        Handles.DrawWireDisc(
            worldCenter,
            rotation * Vector3.up,
            radius);

        // YZ 平面
        Handles.DrawWireDisc(
            worldCenter,
            rotation * Vector3.right,
            radius);
    }


    // =========================================================
    // Draw Capsule Handles
    // =========================================================

    private static void
        DrawCapsuleHandles(
            CapsuleCollider capsule)
    {
        Transform transform =
            capsule.transform;

        Vector3 lossyScale =
            transform.lossyScale;

        float radius =
            capsule.radius *
            Mathf.Max(
                Mathf.Abs(lossyScale.x),
                Mathf.Abs(lossyScale.y),
                Mathf.Abs(lossyScale.z));

        float height =
            capsule.height *
            Mathf.Abs(
                lossyScale[
                    capsule.direction]);

        Vector3 axis;

        Vector3 sideA;

        Vector3 sideB;

        switch (capsule.direction)
        {
            case 0:
                axis = Vector3.right;
                sideA = Vector3.up;
                sideB = Vector3.forward;
                break;

            case 1:
            default:
                axis = Vector3.up;
                sideA = Vector3.right;
                sideB = Vector3.forward;
                break;

            case 2:
                axis = Vector3.forward;
                sideA = Vector3.right;
                sideB = Vector3.up;
                break;
        }

        Quaternion rotation =
            transform.rotation;

        Vector3 worldAxis =
            rotation * axis;

        Vector3 center =
            transform.TransformPoint(
                capsule.center);

        float halfHeight =
            Mathf.Max(
                0f,
                height * 0.5f -
                radius);

        Vector3 top =
            center +
            worldAxis * halfHeight;

        Vector3 bottom =
            center -
            worldAxis * halfHeight;

        Handles.matrix =
            Matrix4x4.identity;

        // 两端半球（用圆盘模拟）
        Vector3 worldSideA =
            rotation * sideA;

        Vector3 worldSideB =
            rotation * sideB;

        Handles.DrawWireDisc(
            top,
            worldAxis,
            radius);

        Handles.DrawWireDisc(
            top,
            worldSideA,
            radius);

        Handles.DrawWireDisc(
            top,
            worldSideB,
            radius);

        Handles.DrawWireDisc(
            bottom,
            worldAxis,
            radius);

        Handles.DrawWireDisc(
            bottom,
            worldSideA,
            radius);

        Handles.DrawWireDisc(
            bottom,
            worldSideB,
            radius);

        // 中间圆柱 4 条连线
        Handles.DrawLine(
            top + worldSideA * radius,
            bottom + worldSideA * radius);

        Handles.DrawLine(
            top - worldSideA * radius,
            bottom - worldSideA * radius);

        Handles.DrawLine(
            top + worldSideB * radius,
            bottom + worldSideB * radius);

        Handles.DrawLine(
            top - worldSideB * radius,
            bottom - worldSideB * radius);
    }


    // =========================================================
    // Deep Find
    //
    // 递归查找 Transform 树中第一个同名节点
    // =========================================================

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
}