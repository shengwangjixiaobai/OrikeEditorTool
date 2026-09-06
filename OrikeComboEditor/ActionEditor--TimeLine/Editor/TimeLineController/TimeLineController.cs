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

    public event Action<BaseClipData> OnSelectionChanged;

    public event Action<IReadOnlyList<BaseClipData>>
        OnMultiSelectionChanged;

    public event Action<BaseClipData> OnClipDataChanged;

    public event Action OnStructureChanged;


    // =========================================================
    // Clipboard
    // =========================================================

    private class ClipboardItem
    {
        public BaseClipData Clip;
        public float RelativeStart;
    }

    private static readonly List<ClipboardItem> Clipboard =
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
    // Select
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
            _selectedClips.Add(clipData);
        }
        else
        {
            if (_selectedClips.Contains(clipData))
            {
                _selectedClips.Remove(clipData);
            }
            else
            {
                _selectedClips.Add(clipData);
            }
        }

        SelectedClip =
            _selectedClips.Count > 0
                ? _selectedClips[_selectedClips.Count - 1]
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

        OnSelectionChanged?.Invoke(null);

        OnMultiSelectionChanged?.Invoke(
            _selectedClips);
    }


    public bool IsSelected(
        BaseClipData clipData)
    {
        return clipData != null &&
               _selectedClips.Contains(clipData);
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

        foreach (TrackData track
                 in _actionData.Tracks)
        {
            if (track == null ||
                track.Clips == null)
            {
                continue;
            }

            for (int i = track.Clips.Count - 1;
                 i >= 0;
                 i--)
            {
                BaseClipData clip =
                    track.Clips[i];

                if (!_selectedClips.Contains(clip))
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

        OnSelectionChanged?.Invoke(null);

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

        if (!_selectedClips.Contains(clipData))
        {
            SelectClip(clipData);
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

        foreach (BaseClipData clip
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

        foreach (BaseClipData clip
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
            targetTrack.Clips == null ||
            Clipboard.Count == 0)
        {
            return;
        }

        _selectedClips.Clear();

        foreach (ClipboardItem item
                 in Clipboard)
        {
            if (item == null ||
                item.Clip == null)
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

        foreach (TrackData track
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
            is AnimationClipData animationClipData)
        {
            if (animationClipData.Animation != null)
            {
                return animationClipData.Animation.length;
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
            is AnimationClipData animationClipData)
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


    private void MarkDirty()
    {
        if (_actionData == null)
        {
            return;
        }

        EditorUtility.SetDirty(
            _actionData);
    }
}