using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System;

[CustomEditor(typeof(ActionData))]
public class ActionDataEditor : Editor
{
    private ActionData _actionData;

    // 每个 Track 折叠状态记录
    private readonly Dictionary<TrackData, bool> _trackFoldouts =
        new Dictionary<TrackData, bool>();


    // =========================================================
    // Enable
    // =========================================================

    private void OnEnable()
    {
        _actionData = target as ActionData;

        if (_actionData == null)
        {
            return;
        }

        InitializeFoldouts();
    }


    // =========================================================
    // Inspector
    // =========================================================

    public override void OnInspectorGUI()
    {
        if (_actionData == null)
        {
            EditorGUILayout.HelpBox(
                "ActionData 无效。",
                MessageType.Error);

            return;
        }

        // -----------------------------------------------------
        // Undo
        // -----------------------------------------------------

        Undo.RecordObject(
            _actionData,
            "Edit Action Data");

        EditorGUILayout.Space(4);

        DrawTracks();

        EditorGUILayout.Space(8);

        DrawCreateTrackButtons();

        if (GUI.changed)
        {
            EditorUtility.SetDirty(_actionData);
        }
    }


    // =========================================================
    // Initialize Foldouts
    // =========================================================

    private void InitializeFoldouts()
    {
        if (_actionData.Tracks == null)
        {
            return;
        }

        foreach (TrackData track in _actionData.Tracks)
        {
            if (track == null)
            {
                continue;
            }

            if (!_trackFoldouts.ContainsKey(track))
            {
                _trackFoldouts.Add(
                    track,
                    true);
            }
        }
    }


    // =========================================================
    // Draw Tracks
    // =========================================================

    private void DrawTracks()
    {
        if (_actionData.Tracks == null ||
            _actionData.Tracks.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "当前 ActionData 没有任何 Track。",
                MessageType.Info);

            return;
        }

        // 清理已经失效的 Track
        List<TrackData> invalidTracks =
            new List<TrackData>();

        foreach (
            KeyValuePair<TrackData, bool> pair
            in _trackFoldouts)
        {
            if (pair.Key == null ||
                !_actionData.Tracks.Contains(pair.Key))
            {
                invalidTracks.Add(pair.Key);
            }
        }

        foreach (TrackData track in invalidTracks)
        {
            _trackFoldouts.Remove(track);
        }


        // -----------------------------------------------------
        // Draw
        // -----------------------------------------------------

        for (int i = 0;
             i < _actionData.Tracks.Count;
             i++)
        {
            TrackData track =
                _actionData.Tracks[i];

            if (track == null)
            {
                continue;
            }

            if (!_trackFoldouts.ContainsKey(track))
            {
                _trackFoldouts.Add(
                    track,
                    true);
            }

            DrawTrack(
                track,
                i);

            EditorGUILayout.Space(4);
        }
    }


    // =========================================================
    // Draw Track
    // =========================================================

    private void DrawTrack(
        TrackData track,
        int index)
    {
        if (track == null)
        {
            return;
        }

        EditorGUILayout.BeginVertical(
            EditorStyles.helpBox);


        // -----------------------------------------------------
        // Track Header
        // -----------------------------------------------------

        EditorGUILayout.BeginHorizontal();

        bool foldout =
            _trackFoldouts[track];

        string trackName =
            string.IsNullOrEmpty(track.TrackName)
                ? "Track " + (index + 1)
                : track.TrackName;

        string title =
            trackName +
            " [" +
            track.ClipType +
            "]";


        // 标题（可点击展开/收起的 foldout 标题）
        bool newFoldout =
            EditorGUILayout.Foldout(
                foldout,
                title,
                true);

        if (newFoldout != foldout)
        {
            _trackFoldouts[track] =
                newFoldout;
        }


        // -----------------------------------------------------
        // Delete
        // -----------------------------------------------------

        if (GUILayout.Button(
                "Delete",
                GUILayout.Width(60)))
        {
            DeleteTrack(track);

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            return;
        }

        EditorGUILayout.EndHorizontal();


        // -----------------------------------------------------
        // 折叠内容
        // -----------------------------------------------------

        if (!_trackFoldouts[track])
        {
            EditorGUILayout.EndVertical();

            return;
        }


        EditorGUILayout.Space(4);


        // -----------------------------------------------------
        // Track Name
        // -----------------------------------------------------

        string newTrackName =
            EditorGUILayout.TextField(
                "Track Name",
                track.TrackName);

        if (newTrackName != track.TrackName)
        {
            track.TrackName =
                newTrackName;

            GUI.changed = true;
        }


        // -----------------------------------------------------
        // Clip Type
        // -----------------------------------------------------

        ClipType newClipType =
            (ClipType)EditorGUILayout.EnumPopup(
                "Clip Type",
                track.ClipType);

        if (newClipType != track.ClipType)
        {
            track.ClipType =
                newClipType;

            GUI.changed = true;
        }


        EditorGUILayout.Space(4);


        // -----------------------------------------------------
        // Clips
        // -----------------------------------------------------

        DrawClips(track);


        EditorGUILayout.Space(4);


        // -----------------------------------------------------
        // Add Clip
        // -----------------------------------------------------

        DrawAddClipButton(track);


        EditorGUILayout.EndVertical();
    }


    // =========================================================
    // Draw Clips
    // =========================================================

    private void DrawClips(
        TrackData track)
    {
        if (track.Clips == null)
        {
            track.Clips =
                new List<BaseClipData>();
        }

        if (track.Clips.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "当前 Track 没有任何 Clip。",
                MessageType.Info);

            return;
        }

        for (int i = 0;
             i < track.Clips.Count;
             i++)
        {
            BaseClipData clip =
                track.Clips[i];

            if (clip == null)
            {
                continue;
            }

            DrawClip(
                track,
                clip,
                i);
        }
    }


    // =========================================================
    // Draw Clip
    // =========================================================

    private void DrawClip(
        TrackData track,
        BaseClipData clip,
        int index)
    {
        EditorGUILayout.BeginVertical(
            EditorStyles.helpBox);


        // -----------------------------------------------------
        // Header
        // -----------------------------------------------------

        EditorGUILayout.BeginHorizontal();

        string clipTitle =
            GetClipTitle(
                clip,
                index);

        EditorGUILayout.LabelField(
            clipTitle,
            EditorStyles.boldLabel);


        if (GUILayout.Button(
                "Delete",
                GUILayout.Width(60)))
        {
            DeleteClip(
                track,
                clip);

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            return;
        }

        EditorGUILayout.EndHorizontal();


        EditorGUILayout.Space(2);


        // -----------------------------------------------------
        // Start Time
        // -----------------------------------------------------

        float newStartTime =
            EditorGUILayout.FloatField(
                "Start Time",
                clip.StartTime);

        if (!Mathf.Approximately(
                newStartTime,
                clip.StartTime))
        {
            clip.StartTime =
                Mathf.Max(
                    0f,
                    newStartTime);

            UpdateClipEndTime(
                clip);

            GUI.changed = true;
        }


        // -----------------------------------------------------
        // Length
        // -----------------------------------------------------

        float newLength =
            EditorGUILayout.FloatField(
                "Length",
                clip.Length);

        if (!Mathf.Approximately(
                newLength,
                clip.Length))
        {
            clip.Length =
                Mathf.Max(
                    0.01f,
                    newLength);

            UpdateClipEndTime(
                clip);

            GUI.changed = true;
        }


        // -----------------------------------------------------
        // End Time
        // -----------------------------------------------------

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.FloatField(
                "End Time",
                clip.EndTime);
        }


        EditorGUILayout.Space(2);


        // -----------------------------------------------------
        // Animation Clip
        // -----------------------------------------------------

        if (clip is AnimationClipData)
        {
            DrawAnimationClip(
                clip as AnimationClipData);
        }


        // -----------------------------------------------------
        // Voice Clip
        // -----------------------------------------------------

        else if (clip is VoiceClipData)
        {
            DrawVoiceClip(
                clip as VoiceClipData);
        }


        // -----------------------------------------------------
        // Effect Clip
        // -----------------------------------------------------

        else if (clip is EffectClipData)
        {
            DrawEffectClip(
                clip as EffectClipData);
        }


        // -----------------------------------------------------
        // Hitbox / Behitbox Clip
        // （BehitboxClipData 继承 HitboxClipData，共用此分支）
        // -----------------------------------------------------

        else if (clip is HitboxClipData)
        {
            DrawHitboxClip(
                clip as HitboxClipData);
        }


        // -----------------------------------------------------
        // State Event Clip
        // -----------------------------------------------------

        else if (clip is StateEventData)
        {
            DrawStateEventClip(
                clip as StateEventData);
        }


        // -----------------------------------------------------
        // Unknown
        // -----------------------------------------------------

        else
        {
            EditorGUILayout.HelpBox(
                "未知的 Clip 类型。",
                MessageType.Warning);
        }


        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(2);
    }


    // =========================================================
    // Animation Clip
    // =========================================================

    private void DrawAnimationClip(
        AnimationClipData clip)
    {
        if (clip == null)
        {
            return;
        }

        AnimationClip newAnimation =
            (AnimationClip)EditorGUILayout.ObjectField(
                "Animation",
                clip.Animation,
                typeof(AnimationClip),
                false);

        if (newAnimation != clip.Animation)
        {
            clip.Animation =
                newAnimation;

            RefreshClip(
                clip);

            GUI.changed = true;
        }
    }


    // =========================================================
    // Voice Clip
    // =========================================================

    private void DrawVoiceClip(
        VoiceClipData clip)
    {
        if (clip == null)
        {
            return;
        }

        AudioClip newVoice =
            (AudioClip)EditorGUILayout.ObjectField(
                "Voice",
                clip.Voice,
                typeof(AudioClip),
                false);

        if (newVoice != clip.Voice)
        {
            clip.Voice =
                newVoice;

            RefreshClip(
                clip);

            GUI.changed = true;
        }
    }


    // =========================================================
    // Effect Clip
    // =========================================================

    private void DrawEffectClip(
        EffectClipData clip)
    {
        if (clip == null)
        {
            return;
        }

        GameObject newEffect =
            (GameObject)EditorGUILayout.ObjectField(
                "Effect",
                clip.EffectPrefab,
                typeof(GameObject),
                false);

        if (newEffect != clip.EffectPrefab)
        {
            clip.EffectPrefab =
                newEffect;

            RefreshClip(
                clip);

            GUI.changed = true;
        }


        // -----------------------------------------------------
        // Attach Bone
        // -----------------------------------------------------

        string newAttachBone =
            EditorGUILayout.TextField(
                "Attach Bone",
                clip.AttachBone);

        if (newAttachBone != clip.AttachBone)
        {
            clip.AttachBone =
                newAttachBone;

            GUI.changed = true;
        }


        // -----------------------------------------------------
        // Local Offset
        // -----------------------------------------------------

        Vector3 newLocalOffset =
            EditorGUILayout.Vector3Field(
                "Local Offset",
                clip.LocalOffset);

        if (newLocalOffset != clip.LocalOffset)
        {
            clip.LocalOffset =
                newLocalOffset;

            GUI.changed = true;
        }
    }


    // =========================================================
    // Hitbox / Behitbox Clip
    // =========================================================

    private void DrawHitboxClip(
        HitboxClipData clip)
    {
        if (clip == null)
        {
            return;
        }

        GameObject newHitbox =
            (GameObject)EditorGUILayout.ObjectField(
                "Hitbox",
                clip.HitboxPrefab,
                typeof(GameObject),
                false);

        if (newHitbox != clip.HitboxPrefab)
        {
            clip.HitboxPrefab =
                newHitbox;

            RefreshClip(
                clip);

            GUI.changed = true;
        }


        // -----------------------------------------------------
        // Attach Bone
        // -----------------------------------------------------

        string newAttachBone =
            EditorGUILayout.TextField(
                "Attach Bone",
                clip.AttachBone);

        if (newAttachBone != clip.AttachBone)
        {
            clip.AttachBone =
                newAttachBone;

            GUI.changed = true;
        }


        // -----------------------------------------------------
        // Local Offset
        // -----------------------------------------------------

        Vector3 newLocalOffset =
            EditorGUILayout.Vector3Field(
                "Local Offset",
                clip.LocalOffset);

        if (newLocalOffset != clip.LocalOffset)
        {
            clip.LocalOffset =
                newLocalOffset;

            GUI.changed = true;
        }
    }


    // =========================================================
    // State Event Clip
    // =========================================================

    private void DrawStateEventClip(
        StateEventData clip)
    {
        if (clip == null)
        {
            return;
        }


        // Event Type
        EventType[] stateTypes =
            EventFactory.GetStateEventTypes();

        int selectedIndex =
            0;

        for (int i = 0;
             i < stateTypes.Length;
             i++)
        {
            if (stateTypes[i] ==
                clip.EventType)
            {
                selectedIndex = i;
                break;
            }
        }

        string[] typeNames =
            new string[stateTypes.Length];

        for (int i = 0;
             i < stateTypes.Length;
             i++)
        {
            typeNames[i] =
                stateTypes[i].ToString();
        }

        int newIndex =
            EditorGUILayout.Popup(
                "Event Type",
                selectedIndex,
                typeNames);

        if (newIndex != selectedIndex)
        {
            clip.EventType =
                stateTypes[newIndex];

            clip.CreateEventInstance();

            GUI.changed = true;
        }


        // 事件字段
        if (clip.StateEvent != null)
        {
            DrawStateEventFieldsIMGUI(
                clip.StateEvent);
        }
    }


    // IMGUI 下用反射绘制事件字段（简化版）
    private void DrawStateEventFieldsIMGUI(
        object eventInstance)
    {
        if (eventInstance == null)
        {
            return;
        }

        Type type =
            eventInstance.GetType();

        System.Reflection.FieldInfo[] fields =
            type.GetFields(
                System.Reflection
                    .BindingFlags.Instance |
                System.Reflection
                    .BindingFlags.Public |
                System.Reflection
                    .BindingFlags.NonPublic);

        foreach (
            System.Reflection.FieldInfo field
            in fields)
        {
            bool isSerialized =
                field.IsPublic ||
                Attribute.IsDefined(
                    field,
                    typeof(
                        SerializeField));

            if (!isSerialized)
            {
                continue;
            }

            if (Attribute.IsDefined(
                    field,
                    typeof(
                        HideInInspector)))
            {
                continue;
            }

            string label =
                ObjectNames.NicifyVariableName(
                    field.Name);

            Type ft =
                field.FieldType;

            if (ft == typeof(int))
            {
                int v =
                    (int)field.GetValue(
                        eventInstance);

                int nv =
                    EditorGUILayout.IntField(
                        label,
                        v);

                if (nv != v)
                {
                    field.SetValue(
                        eventInstance,
                        nv);

                    GUI.changed = true;
                }
            }
            else if (ft == typeof(float))
            {
                float v =
                    (float)field.GetValue(
                        eventInstance);

                float nv =
                    EditorGUILayout.FloatField(
                        label,
                        v);

                if (nv != v)
                {
                    field.SetValue(
                        eventInstance,
                        nv);

                    GUI.changed = true;
                }
            }
            else if (ft == typeof(bool))
            {
                bool v =
                    (bool)field.GetValue(
                        eventInstance);

                bool nv =
                    EditorGUILayout.Toggle(
                        label,
                        v);

                if (nv != v)
                {
                    field.SetValue(
                        eventInstance,
                        nv);

                    GUI.changed = true;
                }
            }
            else if (ft == typeof(string))
            {
                string v =
                    (string)field.GetValue(
                        eventInstance)
                    ?? string.Empty;

                string nv =
                    EditorGUILayout.TextField(
                        label,
                        v);

                if (nv != v)
                {
                    field.SetValue(
                        eventInstance,
                        nv);

                    GUI.changed = true;
                }
            }
            else if (ft == typeof(Vector3))
            {
                Vector3 v =
                    (Vector3)field.GetValue(
                        eventInstance);

                Vector3 nv =
                    EditorGUILayout.Vector3Field(
                        label,
                        v);

                if (nv != v)
                {
                    field.SetValue(
                        eventInstance,
                        nv);

                    GUI.changed = true;
                }
            }
            else
            {
                EditorGUILayout.LabelField(
                    label,
                    $"<{ft.Name}>");
            }
        }
    }


    // =========================================================
    // Add Clip Button
    // =========================================================

    private void DrawAddClipButton(
        TrackData track)
    {
        if (track == null)
        {
            return;
        }

        string buttonText;

        if (track.ClipType ==
            ClipType.Animation)
        {
            buttonText =
                "+ Add Animation Clip";
        }
        else if (track.ClipType ==
                 ClipType.Voice)
        {
            buttonText =
                "+ Add Voice Clip";
        }
        else if (track.ClipType ==
                 ClipType.Effect)
        {
            buttonText =
                "+ Add Effect Clip";
        }
        else if (track.ClipType ==
                 ClipType.Hitbox)
        {
            buttonText =
                "+ Add Hitbox Clip";
        }
        else if (track.ClipType ==
                 ClipType.Behitbox)
        {
            buttonText =
                "+ Add Behitbox Clip";
        }
        else if (track.ClipType ==
                 ClipType.StateEvent)
        {
            buttonText =
                "+ Add State Event Clip";
        }
        else
        {
            buttonText =
                "+ Add Clip";
        }


        if (!GUILayout.Button(
                buttonText,
                GUILayout.Height(24)))
        {
            return;
        }


        // -----------------------------------------------------
        // 确保 List 已初始化
        // -----------------------------------------------------

        if (track.Clips == null)
        {
            track.Clips =
                new List<BaseClipData>();
        }


        // -----------------------------------------------------
        // 创建 Clip 实例
        // 根据轨道类型创建对应的 ClipData
        // 直接 new 即可，SerializeReference 字段由 Unity 自动序列化
        // -----------------------------------------------------

        BaseClipData newClip = null;

        if (track.ClipType ==
            ClipType.Animation)
        {
            newClip =
                new AnimationClipData();
        }
        else if (track.ClipType ==
                 ClipType.Voice)
        {
            newClip =
                new VoiceClipData();
        }
        else if (track.ClipType ==
                 ClipType.Effect)
        {
            newClip =
                new EffectClipData();
        }
        else if (track.ClipType ==
                 ClipType.Hitbox)
        {
            newClip =
                new HitboxClipData();
        }
        else if (track.ClipType ==
                 ClipType.Behitbox)
        {
            newClip =
                new BehitboxClipData();
        }
        else if (track.ClipType ==
                 ClipType.StateEvent)
        {
            newClip =
                new StateEventData();
        }


        if (newClip == null)
        {
            return;
        }


        // -----------------------------------------------------
        // 默认值
        // -----------------------------------------------------

        newClip.StartTime =
            0f;

        newClip.Length =
            1f;

        UpdateClipEndTime(
            newClip);


        // -----------------------------------------------------
        // 添加到列表
        // -----------------------------------------------------

        track.Clips.Add(
            newClip);


        GUI.changed = true;

        EditorUtility.SetDirty(
            _actionData);
    }


    // =========================================================
    // Add Track Buttons
    // =========================================================

    private void DrawCreateTrackButtons()
    {
        EditorGUILayout.LabelField(
            "Create Track",
            EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();


        if (GUILayout.Button(
                "Animation Track",
                GUILayout.Height(26)))
        {
            AddTrack(
                ClipType.Animation);
        }


        if (GUILayout.Button(
                "Voice Track",
                GUILayout.Height(26)))
        {
            AddTrack(
                ClipType.Voice);
        }


        if (GUILayout.Button(
                "Effect Track",
                GUILayout.Height(26)))
        {
            AddTrack(
                ClipType.Effect);
        }


        if (GUILayout.Button(
                "Hitbox Track",
                GUILayout.Height(26)))
        {
            AddTrack(
                ClipType.Hitbox);
        }


        if (GUILayout.Button(
                "Behitbox Track",
                GUILayout.Height(26)))
        {
            AddTrack(
                ClipType.Behitbox);
        }


        if (GUILayout.Button(
                "State Event Track",
                GUILayout.Height(26)))
        {
            AddTrack(
                ClipType.StateEvent);
        }


        EditorGUILayout.EndHorizontal();
    }


    // =========================================================
    // Add Track
    // =========================================================

    private void AddTrack(
        ClipType clipType)
    {
        if (_actionData.Tracks == null)
        {
            _actionData.Tracks =
                new List<TrackData>();
        }


        TrackData newTrack =
            new TrackData();

        newTrack.TrackName =
            GetNewTrackName(
                clipType);

        newTrack.ClipType =
            clipType;

        newTrack.Clips =
            new List<BaseClipData>();


        _actionData.Tracks.Add(
            newTrack);


        _trackFoldouts[newTrack] =
            true;


        GUI.changed = true;

        EditorUtility.SetDirty(
            _actionData);
    }


    // =========================================================
    // New Track Name
    // =========================================================

    private string GetNewTrackName(
        ClipType clipType)
    {
        string prefix;

        if (clipType ==
            ClipType.Voice)
        {
            prefix =
                "Voice Track ";
        }
        else if (clipType ==
                 ClipType.Effect)
        {
            prefix =
                "Effect Track ";
        }
        else if (clipType ==
                 ClipType.Hitbox)
        {
            prefix =
                "Hitbox Track ";
        }
        else if (clipType ==
                 ClipType.Behitbox)
        {
            prefix =
                "Behitbox Track ";
        }
        else if (clipType ==
                 ClipType.StateEvent)
        {
            prefix =
                "State Event Track ";
        }
        else
        {
            prefix =
                "Animation Track ";
        }


        int number = 1;

        while (true)
        {
            string name =
                prefix +
                number;

            bool exists =
                false;


            if (_actionData.Tracks != null)
            {
                foreach (
                    TrackData track
                    in _actionData.Tracks)
                {
                    if (track == null)
                    {
                        continue;
                    }

                    if (track.TrackName ==
                        name)
                    {
                        exists = true;

                        break;
                    }
                }
            }


            if (!exists)
            {
                return name;
            }

            number++;
        }
    }


    // =========================================================
    // Delete Track
    // =========================================================

    private void DeleteTrack(
        TrackData track)
    {
        if (track == null)
        {
            return;
        }


        bool confirm =
            EditorUtility.DisplayDialog(
                "Delete Track",
                "确定要删除这个 Track 吗？\n轨道中的所有 Clip 也会被一起删除。",
                "Delete",
                "Cancel");


        if (!confirm)
        {
            return;
        }


        Undo.RecordObject(
            _actionData,
            "Delete Track");


        _trackFoldouts.Remove(
            track);


        _actionData.Tracks.Remove(
            track);


        GUI.changed = true;

        EditorUtility.SetDirty(
            _actionData);
    }


    // =========================================================
    // Delete Clip
    // =========================================================

    private void DeleteClip(
        TrackData track,
        BaseClipData clip)
    {
        if (track == null ||
            clip == null ||
            track.Clips == null)
        {
            return;
        }


        bool confirm =
            EditorUtility.DisplayDialog(
                "Delete Clip",
                "确定要删除这个 Clip 吗？",
                "Delete",
                "Cancel");


        if (!confirm)
        {
            return;
        }


        Undo.RecordObject(
            _actionData,
            "Delete Clip");


        track.Clips.Remove(
            clip);


        GUI.changed = true;

        EditorUtility.SetDirty(
            _actionData);
    }


    // =========================================================
    // Refresh Clip
    // =========================================================

    private void RefreshClip(
        BaseClipData clip)
    {
        if (clip == null)
        {
            return;
        }

        if (clip is AnimationClipData)
        {
            AnimationClipData animationClip =
                clip as AnimationClipData;

            if (animationClip.Animation != null)
            {
                animationClip.Length =
                    animationClip.Animation.length;
            }
        }
        else if (clip is VoiceClipData)
        {
            VoiceClipData voiceClip =
                clip as VoiceClipData;

            if (voiceClip.Voice != null)
            {
                voiceClip.Length =
                    voiceClip.Voice.length;
            }
        }
        else if (clip is EffectClipData)
        {
            EffectClipData effectClip =
                clip as EffectClipData;

            effectClip.RefreshData();
        }
        else if (clip is HitboxClipData)
        {
            HitboxClipData hitboxClip =
                clip as HitboxClipData;

            hitboxClip.RefreshData();
        }
        else if (clip is StateEventData)
        {
            StateEventData stateClip =
                clip as StateEventData;

            stateClip.RefreshData();
        }

        UpdateClipEndTime(
            clip);
    }


    // =========================================================
    // Update End Time
    // =========================================================

    private void UpdateClipEndTime(
        BaseClipData clip)
    {
        if (clip == null)
        {
            return;
        }

        clip.EndTime =
            clip.StartTime +
            clip.Length;
    }


    // =========================================================
    // Clip Title
    // =========================================================

    private string GetClipTitle(
        BaseClipData clip,
        int index)
    {
        if (clip == null)
        {
            return
                "Clip " +
                (index + 1);
        }


        // =====================================================
        // 优先显示在 Timeline 中设置过的自定义名字
        // =====================================================

        if (!string.IsNullOrEmpty(
                clip.Name))
        {
            return
                clip.Name;
        }


        // =====================================================
        // Animation Clip
        // =====================================================

        if (clip is AnimationClipData)
        {
            AnimationClipData animationClip =
                clip as AnimationClipData;


            if (animationClip.Animation != null)
            {
                return
                    animationClip.Animation.name;
            }


            return
                "Animation Clip " +
                (index + 1);
        }


        // =====================================================
        // Voice Clip
        // =====================================================

        if (clip is VoiceClipData)
        {
            VoiceClipData voiceClip =
                clip as VoiceClipData;


            if (voiceClip.Voice != null)
            {
                return
                    voiceClip.Voice.name;
            }


            return
                "Voice Clip " +
                (index + 1);
        }


        // =====================================================
        // Effect Clip
        // =====================================================

        if (clip is EffectClipData)
        {
            EffectClipData effectClip =
                clip as EffectClipData;


            if (effectClip.EffectPrefab != null)
            {
                return
                    effectClip.EffectPrefab.name;
            }


            return
                "Effect Clip " +
                (index + 1);
        }


        // =====================================================
        // Behitbox Clip（先判断子类）
        // =====================================================

        if (clip is BehitboxClipData)
        {
            BehitboxClipData behitboxClip =
                clip as BehitboxClipData;


            if (behitboxClip.HitboxPrefab != null)
            {
                return
                    behitboxClip.HitboxPrefab.name;
            }


            return
                "Behitbox Clip " +
                (index + 1);
        }


        // =====================================================
        // Hitbox Clip
        // =====================================================

        if (clip is HitboxClipData)
        {
            HitboxClipData hitboxClip =
                clip as HitboxClipData;


            if (hitboxClip.HitboxPrefab != null)
            {
                return
                    hitboxClip.HitboxPrefab.name;
            }


            return
                "Hitbox Clip " +
                (index + 1);
        }


        // =====================================================
        // State Event Clip
        // =====================================================

        if (clip is StateEventData)
        {
            StateEventData stateClip =
                clip as StateEventData;


            if (stateClip.StateEvent != null)
            {
                return
                    stateClip.StateEvent
                        .GetType()
                        .Name;
            }


            return
                "State Event Clip " +
                (index + 1);
        }


        return
            "Clip " +
            (index + 1);
    }
}