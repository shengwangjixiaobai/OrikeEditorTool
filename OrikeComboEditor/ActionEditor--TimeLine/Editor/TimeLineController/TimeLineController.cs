using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class TimeLineController
{
    private readonly ActionData _actionData;
    private readonly float _fps;

    public BaseClipData SelectedClip
    {
        get;
        private set;
    }

    private readonly List<BaseClipData> _selectedClips =
        new List<BaseClipData>();

    public IReadOnlyList<BaseClipData> SelectedClips =>
        _selectedClips;

    public event Action<BaseClipData>
        OnSelectionChanged;

    public event Action<IReadOnlyList<BaseClipData>>
        OnMultiSelectionChanged;

    public event Action<BaseClipData>
        OnClipDataChanged;

    public event Action
        OnStructureChanged;


    // =========================================================
    // Clipboard
    // =========================================================

    private class ClipboardItem
    {
        public BaseClipData Clip;
        public float RelativeStart;
    }

    private static readonly List<ClipboardItem>
        Clipboard =
            new List<ClipboardItem>();

    public bool HasClipboard =>
        Clipboard.Count > 0;


    // =========================================================
    // Constructor
    // =========================================================

    public TimeLineController(
        ActionData actionData,
        float fps)
    {
        _actionData = actionData;
        _fps = fps;
    }


    // =========================================================
    // Selection
    // =========================================================

    public void SelectClip(
        BaseClipData clipData)
    {
        SelectClip(
            clipData,
            false);
    }

    public void SelectClip(
        BaseClipData clipData,
        bool additive)
    {
        if (clipData == null)
        {
            ClearSelection();
            return;
        }

        if (!additive)
        {
            _selectedClips.Clear();

            _selectedClips.Add(
                clipData);
        }
        else
        {
            if (_selectedClips.Contains(
                    clipData))
            {
                _selectedClips.Remove(
                    clipData);
            }
            else
            {
                _selectedClips.Add(
                    clipData);
            }
        }

        SelectedClip =
            _selectedClips.Count > 0
                ? _selectedClips[
                    _selectedClips.Count - 1]
                : null;

        OnSelectionChanged?.Invoke(
            SelectedClip);

        OnMultiSelectionChanged?.Invoke(
            _selectedClips);
    }


    public void ClearSelection()
    {
        if (_selectedClips.Count == 0 &&
            SelectedClip == null)
        {
            return;
        }

        _selectedClips.Clear();

        SelectedClip = null;

        OnSelectionChanged?.Invoke(
            null);

        OnMultiSelectionChanged?.Invoke(
            _selectedClips);
    }


    public bool IsSelected(
        BaseClipData clipData)
    {
        return clipData != null &&
               _selectedClips.Contains(
                   clipData);
    }


    // =========================================================
    // Move Clip
    // =========================================================

    public void MoveClip(
        BaseClipData clipData,
        float deltaTime)
    {
        if (clipData == null)
        {
            return;
        }

        float newStartTime =
            clipData.StartTime +
            deltaTime;

        newStartTime =
            Mathf.Max(
                0f,
                newStartTime);

        clipData.StartTime =
            newStartTime;

        clipData.EndTime =
            clipData.StartTime +
            clipData.Length;

        NotifyClipChanged(
            clipData);
    }


    // =========================================================
    // Resize Left
    // =========================================================

    public void ResizeClipLeft(
        BaseClipData clipData,
        float deltaTime)
    {
        if (clipData == null)
        {
            return;
        }

        float oldEndTime =
            clipData.EndTime;

        float newStartTime =
            clipData.StartTime +
            deltaTime;

        newStartTime =
            Mathf.Max(
                0f,
                newStartTime);

        float newLength =
            oldEndTime -
            newStartTime;

        float minLength =
            GetMinLength();

        float maxLength =
            GetMaxLength(
                clipData);

        newLength =
            Mathf.Clamp(
                newLength,
                minLength,
                maxLength);

        newStartTime =
            oldEndTime -
            newLength;

        newStartTime =
            Mathf.Max(
                0f,
                newStartTime);

        clipData.StartTime =
            newStartTime;

        clipData.Length =
            newLength;

        clipData.EndTime =
            oldEndTime;

        NotifyClipChanged(
            clipData);
    }


    // =========================================================
    // Resize Right
    // =========================================================

    public void ResizeClipRight(
        BaseClipData clipData,
        float deltaTime)
    {
        if (clipData == null)
        {
            return;
        }

        float newLength =
            clipData.Length +
            deltaTime;

        float minLength =
            GetMinLength();

        float maxLength =
            GetMaxLength(
                clipData);

        newLength =
            Mathf.Clamp(
                newLength,
                minLength,
                maxLength);

        clipData.Length =
            newLength;

        clipData.EndTime =
            clipData.StartTime +
            clipData.Length;

        NotifyClipChanged(
            clipData);
    }


    // =========================================================
    // Delete
    // =========================================================

    public void DeleteSelectedClips()
    {
        if (_actionData == null ||
            _actionData.Tracks == null)
        {
            return;
        }

        if (_selectedClips.Count == 0)
        {
            return;
        }

        bool changed = false;

        foreach (
            TrackData track
            in _actionData.Tracks)
        {
            if (track == null ||
                track.Clips == null)
            {
                continue;
            }

            for (
                int i = track.Clips.Count - 1;
                i >= 0;
                i--)
            {
                BaseClipData clip =
                    track.Clips[i];

                if (!_selectedClips.Contains(
                        clip))
                {
                    continue;
                }

                track.Clips.RemoveAt(i);

                changed = true;
            }
        }

        if (!changed)
        {
            return;
        }

        _selectedClips.Clear();

        SelectedClip = null;

        MarkDirty();

        OnSelectionChanged?.Invoke(
            null);

        OnMultiSelectionChanged?.Invoke(
            _selectedClips);

        OnStructureChanged?.Invoke();
    }


    public void DeleteClip(
        BaseClipData clipData)
    {
        if (clipData == null)
        {
            return;
        }

        if (!_selectedClips.Contains(
                clipData))
        {
            SelectClip(
                clipData);
        }

        DeleteSelectedClips();
    }


    // =========================================================
    // Copy
    // =========================================================

    public void CopySelectedClips()
    {
        Clipboard.Clear();

        if (_selectedClips.Count == 0)
        {
            return;
        }

        float baseStart =
            float.MaxValue;

        foreach (
            BaseClipData clip
            in _selectedClips)
        {
            if (clip == null)
            {
                continue;
            }

            baseStart =
                Mathf.Min(
                    baseStart,
                    clip.StartTime);
        }

        foreach (
            BaseClipData clip
            in _selectedClips)
        {
            if (clip == null)
            {
                continue;
            }

            BaseClipData copy =
                CloneClip(
                    clip);

            if (copy == null)
            {
                continue;
            }

            Clipboard.Add(
                new ClipboardItem
                {
                    Clip = copy,

                    RelativeStart =
                        clip.StartTime -
                        baseStart
                });
        }
    }


    // =========================================================
    // Paste
    // =========================================================

    public void PasteClips(
        TrackData targetTrack,
        float pasteTime)
    {
        if (targetTrack == null ||
            Clipboard.Count == 0)
        {
            return;
        }

        if (targetTrack.Clips == null)
        {
            targetTrack.Clips =
                new List<BaseClipData>();
        }

        _selectedClips.Clear();

        foreach (
            ClipboardItem item
            in Clipboard)
        {
            if (item == null ||
                item.Clip == null)
            {
                continue;
            }

            if (!targetTrack.CanAcceptClip(
                    item.Clip))
            {
                continue;
            }

            BaseClipData newClip =
                CloneClip(
                    item.Clip);

            if (newClip == null)
            {
                continue;
            }

            newClip.StartTime =
                Mathf.Max(
                    0f,
                    pasteTime +
                    item.RelativeStart);

            newClip.EndTime =
                newClip.StartTime +
                newClip.Length;

            targetTrack.Clips.Add(
                newClip);

            _selectedClips.Add(
                newClip);
        }

        if (_selectedClips.Count == 0)
        {
            return;
        }

        SelectedClip =
            _selectedClips[
                _selectedClips.Count - 1];

        MarkDirty();

        OnSelectionChanged?.Invoke(
            SelectedClip);

        OnMultiSelectionChanged?.Invoke(
            _selectedClips);

        OnStructureChanged?.Invoke();
    }


    // =========================================================
    // Add Track
    // =========================================================

    public TrackData AddTrack(
        ClipType clipType)
    {
        if (_actionData == null)
        {
            return null;
        }

        if (_actionData.Tracks == null)
        {
            _actionData.Tracks =
                new List<TrackData>();
        }

        TrackData track =
            new TrackData();

        track.ClipType =
            clipType;

        track.TrackName =
            GetNewTrackName(
                clipType);

        track.Clips =
            new List<BaseClipData>();

        _actionData.Tracks.Add(
            track);

        MarkDirty();

        OnStructureChanged?.Invoke();

        return track;
    }


    public TrackData AddAnimationTrack()
    {
        return AddTrack(
            ClipType.Animation);
    }


    public TrackData AddVoiceTrack()
    {
        return AddTrack(
            ClipType.Voice);
    }


    // =========================================================
    // Delete Track
    // =========================================================

    public void DeleteTrack(
        TrackData trackData)
    {
        if (_actionData == null ||
            _actionData.Tracks == null ||
            trackData == null)
        {
            return;
        }

        int trackIndex =
            _actionData.Tracks.IndexOf(
                trackData);

        if (trackIndex < 0)
        {
            return;
        }

        Undo.RecordObject(
            _actionData,
            "Delete Track");

        bool selectionChanged = false;

        if (trackData.Clips != null)
        {
            foreach (
                BaseClipData clip
                in trackData.Clips)
            {
                if (clip == null)
                {
                    continue;
                }

                if (_selectedClips.Remove(
                        clip))
                {
                    selectionChanged = true;
                }
            }
        }

        _actionData.Tracks.RemoveAt(
            trackIndex);

        if (SelectedClip != null &&
            !_selectedClips.Contains(
                SelectedClip))
        {
            SelectedClip =
                _selectedClips.Count > 0
                    ? _selectedClips[
                        _selectedClips.Count - 1]
                    : null;

            selectionChanged = true;
        }

        MarkDirty();

        if (selectionChanged)
        {
            OnSelectionChanged?.Invoke(
                SelectedClip);

            OnMultiSelectionChanged?.Invoke(
                _selectedClips);
        }

        OnStructureChanged?.Invoke();
    }


    private string GetNewTrackName(
        ClipType clipType)
    {
        string prefix =
            clipType == ClipType.Voice
                ? "Voice Track "
                : "Animation Track ";

        int index = 1;

        while (true)
        {
            string name =
                prefix + index;

            bool exists = false;

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

            index++;
        }
    }


    // =========================================================
    // Add Clip Check
    // =========================================================

    public bool CanAddClip(
        TrackData trackData,
        ClipType clipType)
    {
        if (trackData == null)
        {
            return false;
        }

        return trackData.CanAcceptClip(
            clipType);
    }


    public bool CanAddClip(
        TrackData trackData,
        BaseClipData clipData)
    {
        if (trackData == null ||
            clipData == null)
        {
            return false;
        }

        return trackData.CanAcceptClip(
            clipData);
    }


    // =========================================================
    // Add Animation Clip
    // =========================================================

    public AnimationClipData AddAnimationClip(
        TrackData trackData,
        AnimationClip animation,
        float startTime)
    {
        if (trackData == null)
        {
            return null;
        }

        if (animation == null)
        {
            return null;
        }

        if (!trackData.CanAcceptClip(
                ClipType.Animation))
        {
            return null;
        }

        if (trackData.Clips == null)
        {
            trackData.Clips =
                new List<BaseClipData>();
        }

        AnimationClipData clip =
            new AnimationClipData();

        clip.Animation =
            animation;

        clip.StartTime =
            Mathf.Max(
                0f,
                startTime);

        clip.Length =
            animation.length;

        clip.EndTime =
            clip.StartTime +
            clip.Length;

        trackData.Clips.Add(
            clip);

        _selectedClips.Clear();

        _selectedClips.Add(
            clip);

        SelectedClip =
            clip;

        MarkDirty();

        OnSelectionChanged?.Invoke(
            clip);

        OnMultiSelectionChanged?.Invoke(
            _selectedClips);

        OnStructureChanged?.Invoke();

        return clip;
    }


    // =========================================================
    // Add Voice Clip
    // =========================================================

    public VoiceClipData AddVoiceClip(
        TrackData trackData,
        AudioClip voice,
        float startTime)
    {
        if (trackData == null)
        {
            return null;
        }

        if (voice == null)
        {
            return null;
        }

        if (!trackData.CanAcceptClip(
                ClipType.Voice))
        {
            return null;
        }

        if (trackData.Clips == null)
        {
            trackData.Clips =
                new List<BaseClipData>();
        }

        VoiceClipData clip =
            new VoiceClipData();

        clip.Voice =
            voice;

        clip.StartTime =
            Mathf.Max(
                0f,
                startTime);

        clip.Length =
            voice.length;

        clip.EndTime =
            clip.StartTime +
            clip.Length;

        trackData.Clips.Add(
            clip);

        _selectedClips.Clear();

        _selectedClips.Add(
            clip);

        SelectedClip =
            clip;

        MarkDirty();

        OnSelectionChanged?.Invoke(
            clip);

        OnMultiSelectionChanged?.Invoke(
            _selectedClips);

        OnStructureChanged?.Invoke();

        return clip;
    }


    // =========================================================
    // Find Track
    // =========================================================

    public TrackData GetTrackForClip(
        BaseClipData clipData)
    {
        if (_actionData == null ||
            _actionData.Tracks == null ||
            clipData == null)
        {
            return null;
        }

        foreach (
            TrackData track
            in _actionData.Tracks)
        {
            if (track == null ||
                track.Clips == null)
            {
                continue;
            }

            if (track.Clips.Contains(
                    clipData))
            {
                return track;
            }
        }

        return null;
    }


    // =========================================================
    // Inspector
    // =========================================================

    public void SetClipStartTime(
        BaseClipData clipData,
        float startTime)
    {
        if (clipData == null)
        {
            return;
        }

        clipData.StartTime =
            Mathf.Max(
                0f,
                startTime);

        clipData.EndTime =
            clipData.StartTime +
            clipData.Length;

        NotifyClipChanged(
            clipData);
    }


    public void SetClipLength(
        BaseClipData clipData,
        float length)
    {
        if (clipData == null)
        {
            return;
        }

        float minLength =
            GetMinLength();

        float maxLength =
            GetMaxLength(
                clipData);

        length =
            Mathf.Clamp(
                length,
                minLength,
                maxLength);

        clipData.Length =
            length;

        clipData.EndTime =
            clipData.StartTime +
            clipData.Length;

        NotifyClipChanged(
            clipData);
    }


    public void SetAnimation(
        AnimationClipData clipData,
        AnimationClip animation)
    {
        if (clipData == null)
        {
            return;
        }

        clipData.Animation =
            animation;

        if (animation != null)
        {
            clipData.Length =
                animation.length;

            clipData.EndTime =
                clipData.StartTime +
                clipData.Length;
        }
        else
        {
            clipData.Length = 0f;

            clipData.EndTime =
                clipData.StartTime;
        }

        NotifyClipChanged(
            clipData);
    }


    public void SetVoice(
        VoiceClipData clipData,
        AudioClip voice)
    {
        if (clipData == null)
        {
            return;
        }

        clipData.Voice =
            voice;

        clipData.RefreshData();

        NotifyClipChanged(
            clipData);
    }


    // =========================================================
    // Min Length
    // =========================================================

    private float GetMinLength()
    {
        if (_fps <= 0f)
        {
            return 0.01f;
        }

        return 1f / _fps;
    }


    // =========================================================
    // Max Length
    // =========================================================

    private float GetMaxLength(
        BaseClipData clipData)
    {
        if (clipData
            is AnimationClipData
                animationClipData)
        {
            if (animationClipData.Animation != null)
            {
                return
                    animationClipData
                        .Animation.length;
            }
        }

        if (clipData
            is VoiceClipData
                voiceClipData)
        {
            if (voiceClipData.Voice != null)
            {
                return
                    voiceClipData
                        .Voice.length;
            }
        }

        return float.MaxValue;
    }


    // =========================================================
    // Clone
    // =========================================================

    private BaseClipData CloneClip(
        BaseClipData source)
    {
        if (source == null)
        {
            return null;
        }

        if (source
            is AnimationClipData
                animationClipData)
        {
            AnimationClipData clone =
                new AnimationClipData();

            clone.Animation =
                animationClipData.Animation;

            clone.StartTime =
                animationClipData.StartTime;

            clone.Length =
                animationClipData.Length;

            clone.EndTime =
                animationClipData.EndTime;

            return clone;
        }

        if (source
            is VoiceClipData
                voiceClipData)
        {
            VoiceClipData clone =
                new VoiceClipData();

            clone.Voice =
                voiceClipData.Voice;

            clone.StartTime =
                voiceClipData.StartTime;

            clone.Length =
                voiceClipData.Length;

            clone.EndTime =
                voiceClipData.EndTime;

            return clone;
        }

        return null;
    }


    // =========================================================
    // Notify
    // =========================================================

    private void NotifyClipChanged(
        BaseClipData clipData)
    {
        MarkDirty();

        OnClipDataChanged?.Invoke(
            clipData);
    }


    // =========================================================
    // Dirty
    // =========================================================

    private void MarkDirty()
    {
        if (_actionData == null)
        {
            return;
        }

        EditorUtility.SetDirty(
            _actionData);
    }


    public void MoveSelectedClips(
        BaseClipData activeClip,
        float deltaTime)
    {
        if (_selectedClips.Count <= 1)
        {
            MoveClip(activeClip, deltaTime);
            return;
        }

        deltaTime = Mathf.Max(
            -GetMinimumSelectedStartTime(),
            deltaTime);

        foreach (BaseClipData clip in _selectedClips)
        {
            if (clip == null)
            {
                continue;
            }

            clip.StartTime += deltaTime;

            clip.StartTime =
                Mathf.Max(
                    0f,
                    clip.StartTime);

            clip.EndTime =
                clip.StartTime +
                clip.Length;

            OnClipDataChanged?.Invoke(
                clip);
        }

        MarkDirty();
    }


    public void ResizeSelectedClipsLeft(
        BaseClipData activeClip,
        float deltaTime)
    {
        if (_selectedClips.Count <= 1)
        {
            ResizeClipLeft(
                activeClip,
                deltaTime);

            return;
        }

        float minLength =
            GetMinLength();

        foreach (BaseClipData clip in _selectedClips)
        {
            if (clip == null)
            {
                continue;
            }

            float oldEnd =
                clip.EndTime;

            float newStart =
                clip.StartTime +
                deltaTime;

            newStart =
                Mathf.Max(
                    0f,
                    newStart);

            float newLength =
                oldEnd -
                newStart;

            float maxLength =
                GetMaxLength(clip);

            newLength =
                Mathf.Clamp(
                    newLength,
                    minLength,
                    maxLength);

            newStart =
                oldEnd -
                newLength;

            newStart =
                Mathf.Max(
                    0f,
                    newStart);

            clip.StartTime =
                newStart;

            clip.Length =
                newLength;

            clip.EndTime =
                oldEnd;

            OnClipDataChanged?.Invoke(
                clip);
        }

        MarkDirty();
    }


    public void ResizeSelectedClipsRight(
        BaseClipData activeClip,
        float deltaTime)
    {
        if (_selectedClips.Count <= 1)
        {
            ResizeClipRight(
                activeClip,
                deltaTime);

            return;
        }

        float minLength =
            GetMinLength();

        foreach (BaseClipData clip in _selectedClips)
        {
            if (clip == null)
            {
                continue;
            }

            float newLength =
                clip.Length +
                deltaTime;

            float maxLength =
                GetMaxLength(clip);

            newLength =
                Mathf.Clamp(
                    newLength,
                    minLength,
                    maxLength);

            clip.Length =
                newLength;

            clip.EndTime =
                clip.StartTime +
                clip.Length;

            OnClipDataChanged?.Invoke(
                clip);
        }

        MarkDirty();
    }


    private float GetMinimumSelectedStartTime()
    {
        float min =
            float.MaxValue;

        foreach (BaseClipData clip in _selectedClips)
        {
            if (clip == null)
            {
                continue;
            }

            min =
                Mathf.Min(
                    min,
                    clip.StartTime);
        }

        if (min == float.MaxValue)
        {
            return 0f;
        }

        return min;
    }


    public void SelectClipFromPointer(
        BaseClipData clipData,
        bool additive)
    {
        if (clipData == null)
        {
            return;
        }

        // Ctrl / Command：
        // 正常进行增加选择
        if (additive)
        {
            SelectClip(
                clipData,
                true);

            return;
        }

        // 没有任何选择：
        // 直接选择
        if (_selectedClips.Count == 0)
        {
            SelectClip(
                clipData,
                false);

            return;
        }

        // 已经选中的 Clip：
        // 保持当前多选
        if (_selectedClips.Contains(
                clipData))
        {
            SelectedClip =
                clipData;

            OnSelectionChanged?.Invoke(
                SelectedClip);

            OnMultiSelectionChanged?.Invoke(
                _selectedClips);

            return;
        }

        // 已经存在多选时，
        // 普通点击另一个 Clip 也加入选择
        _selectedClips.Add(
            clipData);

        SelectedClip =
            clipData;

        OnSelectionChanged?.Invoke(
            SelectedClip);

        OnMultiSelectionChanged?.Invoke(
            _selectedClips);
    }
}