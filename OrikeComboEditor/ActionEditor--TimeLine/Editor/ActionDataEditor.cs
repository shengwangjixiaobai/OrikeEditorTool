using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ActionData))]
public class ActionDataEditor : Editor
{
    private ActionData _actionData;

    // 保存每个 Track 的展开/收起状态
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
                "当前 ActionData 没有 Track。",
                MessageType.Info);

            return;
        }

        // 清理已经被删除的 Track
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


        // 这里使用真正的 foldout 状态
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
        // 收起状态
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
                "当前 Track 没有 Clip。",
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
        else
        {
            buttonText =
                "+ Add Voice Clip";
        }


        if (!GUILayout.Button(
                buttonText,
                GUILayout.Height(24)))
        {
            return;
        }


        // -----------------------------------------------------
        // 确保 List 存在
        // -----------------------------------------------------

        if (track.Clips == null)
        {
            track.Clips =
                new List<BaseClipData>();
        }


        // -----------------------------------------------------
        // 关键：
        // 直接创建正确的具体类型
        // 不再使用 SerializeReference InsertArrayElement
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
        // 添加
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
                "确定要删除这个 Track 吗？\n其中的所有 Clip 也会被删除。",
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
        // 优先使用 Timeline 编辑器配置的名称
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


        return
            "Clip " +
            (index + 1);
    }
}