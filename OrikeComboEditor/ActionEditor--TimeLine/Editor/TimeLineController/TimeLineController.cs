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
    public event Action<TrackData>
        OnTrackDataChanged;


    // =========================================================
    // Point Event Selection
    // =========================================================

    public PointEventData SelectedPointEvent
    {
        get;
        private set;
    }

    public event Action<PointEventData>
        OnPointEventSelectionChanged;

    public event Action<PointEventData>
        OnPointEventChanged;


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

            // 选中 clip 时取消点事件选中
            if (SelectedPointEvent !=
                null)
            {
                SelectedPointEvent =
                    null;

                OnPointEventSelectionChanged?.Invoke(
                    null);
            }
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
        bool clipCleared =
            _selectedClips.Count > 0 ||
            SelectedClip != null;

        _selectedClips.Clear();

        SelectedClip = null;

        if (clipCleared)
        {
            OnSelectionChanged?.Invoke(
                null);

            OnMultiSelectionChanged?.Invoke(
                _selectedClips);
        }

        if (SelectedPointEvent !=
            null)
        {
            SelectedPointEvent =
                null;

            OnPointEventSelectionChanged?.Invoke(
                null);
        }
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


    public TrackData AddEffectTrack()
    {
        return AddTrack(
            ClipType.Effect);
    }


    public TrackData AddHitboxTrack()
    {
        return AddTrack(
            ClipType.Hitbox);
    }


    public TrackData AddBehitboxTrack()
    {
        return AddTrack(
            ClipType.Behitbox);
    }


    public TrackData AddStateEventTrack()
    {
        return AddTrack(
            ClipType.StateEvent);
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
        string prefix;

        if (clipType == ClipType.Voice)
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
    // Add Effect Clip
    // =========================================================

    public EffectClipData AddEffectClip(
        TrackData trackData,
        GameObject effectPrefab,
        float startTime)
    {
        if (trackData == null)
        {
            return null;
        }

        if (effectPrefab == null)
        {
            return null;
        }

        if (!trackData.CanAcceptClip(
                ClipType.Effect))
        {
            return null;
        }

        if (trackData.Clips == null)
        {
            trackData.Clips =
                new List<BaseClipData>();
        }

        EffectClipData clip =
            new EffectClipData();

        clip.EffectPrefab =
            effectPrefab;

        clip.StartTime =
            Mathf.Max(
                0f,
                startTime);

        clip.RefreshData();

        // 没有 ParticleSystem 时
        // RefreshData 会给 1f 默认长度
        if (clip.Length <= 0f)
        {
            clip.Length = 1f;
        }

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
    // Add Hitbox Clip
    // =========================================================

    public HitboxClipData AddHitboxClip(
        TrackData trackData,
        GameObject hitboxPrefab,
        float startTime)
    {
        if (trackData == null)
        {
            return null;
        }

        if (hitboxPrefab == null)
        {
            return null;
        }

        if (!trackData.CanAcceptClip(
                ClipType.Hitbox))
        {
            return null;
        }

        if (trackData.Clips == null)
        {
            trackData.Clips =
                new List<BaseClipData>();
        }

        HitboxClipData clip =
            new HitboxClipData();

        clip.HitboxPrefab =
            hitboxPrefab;

        clip.StartTime =
            Mathf.Max(
                0f,
                startTime);

        clip.RefreshData();

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
    // Add Behitbox Clip
    // =========================================================

    public BehitboxClipData AddBehitboxClip(
        TrackData trackData,
        GameObject hitboxPrefab,
        float startTime)
    {
        if (trackData == null)
        {
            return null;
        }

        if (hitboxPrefab == null)
        {
            return null;
        }

        if (!trackData.CanAcceptClip(
                ClipType.Behitbox))
        {
            return null;
        }

        if (trackData.Clips == null)
        {
            trackData.Clips =
                new List<BaseClipData>();
        }

        BehitboxClipData clip =
            new BehitboxClipData();

        clip.HitboxPrefab =
            hitboxPrefab;

        clip.StartTime =
            Mathf.Max(
                0f,
                startTime);

        clip.RefreshData();

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
    // Add State Event Clip
    // =========================================================

    public StateEventData AddStateEventClip(
        TrackData trackData,
        EventType eventType,
        float startTime)
    {
        if (trackData == null)
        {
            return null;
        }

        if (!trackData.CanAcceptClip(
                ClipType.StateEvent))
        {
            return null;
        }

        if (trackData.Clips == null)
        {
            trackData.Clips =
                new List<BaseClipData>();
        }

        StateEventData clip =
            new StateEventData();

        clip.EventType =
            eventType;

        clip.CreateEventInstance();

        clip.StartTime =
            Mathf.Max(
                0f,
                startTime);

        clip.RefreshData();

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

        SelectedPointEvent =
            null;

        MarkDirty();

        OnSelectionChanged?.Invoke(
            clip);

        OnMultiSelectionChanged?.Invoke(
            _selectedClips);

        OnPointEventSelectionChanged?.Invoke(
            null);

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


    public void SetEffect(
        EffectClipData clipData,
        GameObject effectPrefab)
    {
        if (clipData == null)
        {
            return;
        }

        clipData.EffectPrefab =
            effectPrefab;

        clipData.RefreshData();

        NotifyClipChanged(
            clipData);
    }


    public void SetEffectAttachBone(
        EffectClipData clipData,
        string attachBone)
    {
        if (clipData == null)
        {
            return;
        }

        clipData.AttachBone =
            attachBone ?? string.Empty;

        NotifyClipChanged(
            clipData);
    }


    public void SetEffectLocalOffset(
        EffectClipData clipData,
        Vector3 localOffset)
    {
        if (clipData == null)
        {
            return;
        }

        clipData.LocalOffset =
            localOffset;

        NotifyClipChanged(
            clipData);
    }


    // =========================================================
    // Hitbox / Behitbox Set
    //
    // HitboxClipData 是 BehitboxClipData 的基类
    // 所以这些方法对两种类型都适用
    // =========================================================

    public void SetHitbox(
        HitboxClipData clipData,
        GameObject hitboxPrefab)
    {
        if (clipData == null)
        {
            return;
        }

        clipData.HitboxPrefab =
            hitboxPrefab;

        clipData.RefreshData();

        NotifyClipChanged(
            clipData);
    }


    public void SetHitboxAttachBone(
        HitboxClipData clipData,
        string attachBone)
    {
        if (clipData == null)
        {
            return;
        }

        clipData.AttachBone =
            attachBone ?? string.Empty;

        NotifyClipChanged(
            clipData);
    }


    public void SetHitboxLocalOffset(
        HitboxClipData clipData,
        Vector3 localOffset)
    {
        if (clipData == null)
        {
            return;
        }

        clipData.LocalOffset =
            localOffset;

        NotifyClipChanged(
            clipData);
    }


    // =========================================================
    // State Event
    // =========================================================

    public void SetStateEventType(
        StateEventData clipData,
        EventType eventType)
    {
        if (clipData == null)
        {
            return;
        }

        clipData.EventType =
            eventType;

        clipData.CreateEventInstance();

        NotifyClipChanged(
            clipData);
    }


    /// <summary>
    /// 标记 StateEvent 数据已修改
    /// 用于事件实例字段被反射修改后标记 dirty
    /// </summary>
    public void SetStateEventDirty(
        StateEventData clipData)
    {
        if (clipData == null)
        {
            return;
        }

        MarkDirty();
    }


    // =========================================================
    // Point Event
    // =========================================================

    public PointEventData AddPointEvent(
        TrackData trackData,
        EventType eventType,
        float time)
    {
        if (trackData == null)
        {
            return null;
        }

        if (trackData.PointEvents == null)
        {
            trackData.PointEvents =
                new List<PointEventData>();
        }

        PointEventData pointEvent =
            new PointEventData();

        pointEvent.EventType =
            eventType;

        pointEvent.CreateEventInstance();

        pointEvent.Time =
            Mathf.Max(
                0f,
                time);

        trackData.PointEvents.Add(
            pointEvent);

        // 选中点事件，取消 clip 选中
        _selectedClips.Clear();

        SelectedClip =
            null;

        SelectedPointEvent =
            pointEvent;

        MarkDirty();

        OnSelectionChanged?.Invoke(
            null);

        OnMultiSelectionChanged?.Invoke(
            _selectedClips);

        OnPointEventSelectionChanged?.Invoke(
            pointEvent);

        OnStructureChanged?.Invoke();

        return pointEvent;
    }


    public void SelectPointEvent(
        PointEventData pointEvent)
    {
        // 选中点事件，取消 clip 选中
        _selectedClips.Clear();

        SelectedClip =
            null;

        SelectedPointEvent =
            pointEvent;

        OnSelectionChanged?.Invoke(
            null);

        OnMultiSelectionChanged?.Invoke(
            _selectedClips);

        OnPointEventSelectionChanged?.Invoke(
            pointEvent);
    }


    public void ClearPointEventSelection()
    {
        if (SelectedPointEvent == null)
        {
            return;
        }

        SelectedPointEvent =
            null;

        OnPointEventSelectionChanged?.Invoke(
            null);
    }


    public void SetPointEventType(
        PointEventData pointEvent,
        EventType eventType)
    {
        if (pointEvent == null)
        {
            return;
        }

        pointEvent.EventType =
            eventType;

        pointEvent.CreateEventInstance();

        OnPointEventChanged?.Invoke(
            pointEvent);
    }


    public void SetPointEventTime(
        PointEventData pointEvent,
        float time)
    {
        if (pointEvent == null)
        {
            return;
        }

        pointEvent.Time =
            Mathf.Max(
                0f,
                time);

        // 只通知数据变化，Marker 位置由 TrackView 局部刷新
        // 不触发 OnStructureChanged，避免整个窗口重建导致输入焦点丢失
        OnPointEventChanged?.Invoke(
            pointEvent);
    }


    public void SetPointEventName(
        PointEventData pointEvent,
        string name)
    {
        if (pointEvent == null)
        {
            return;
        }

        pointEvent.Name =
            name;

        OnPointEventChanged?.Invoke(
            pointEvent);
    }


    /// <summary>
    /// 标记 PointEvent 数据已修改
    /// </summary>
    public void SetPointEventDirty(
        PointEventData pointEvent)
    {
        if (pointEvent == null)
        {
            return;
        }

        MarkDirty();
    }


    public void DeletePointEvent(
        TrackData trackData,
        PointEventData pointEvent)
    {
        if (trackData == null ||
            pointEvent == null ||
            trackData.PointEvents == null)
        {
            return;
        }

        trackData.PointEvents.Remove(
            pointEvent);

        if (SelectedPointEvent ==
            pointEvent)
        {
            SelectedPointEvent =
                null;

            OnPointEventSelectionChanged?.Invoke(
                null);
        }

        MarkDirty();

        OnStructureChanged?.Invoke();
    }


    public void SetClipName(
        BaseClipData clipData,
        string clipName)
    {
        if (clipData == null ||
            _actionData == null)
        {
            return;
        }

        Undo.RecordObject(
            _actionData,
            "Change Clip Name");

        clipData.Name = clipName;

        MarkDirty();

        OnClipDataChanged?.Invoke(
            clipData);
    }

    public void SetTrackName(
        TrackData trackData,
        string trackName)
    {
        if (trackData == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(
                trackName))
        {
            trackName =
                "Track";
        }

        Undo.RecordObject(
            _actionData,
            "Change Track Name");

        trackData.TrackName =
            trackName;

        MarkDirty();

        OnTrackDataChanged?.Invoke(
            trackData);
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

        // Effect 没有原生最大长度概念
        // 由用户在 Inspector 里自由调整
        if (clipData
            is EffectClipData)
        {
            return float.MaxValue;
        }

        // Hitbox / Behitbox
        // 由用户在 Inspector 里自由调整
        if (clipData
            is HitboxClipData)
        {
            return float.MaxValue;
        }

        // State Event
        // 由用户在 Inspector 里自由调整
        if (clipData
            is StateEventData)
        {
            return float.MaxValue;
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

            clone.Name =
                source.Name;

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

            clone.Name =
                source.Name;

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

        if (source
            is EffectClipData
                effectClipData)
        {
            EffectClipData clone =
                new EffectClipData();

            clone.Name =
                source.Name;

            clone.EffectPrefab =
                effectClipData.EffectPrefab;

            clone.StartTime =
                effectClipData.StartTime;

            clone.Length =
                effectClipData.Length;

            clone.EndTime =
                effectClipData.EndTime;

            return clone;
        }

        // Behitbox 先判断（HitboxClipData 的子类）
        if (source
            is BehitboxClipData
                behitboxClipData)
        {
            BehitboxClipData clone =
                new BehitboxClipData();

            clone.Name =
                source.Name;

            clone.HitboxPrefab =
                behitboxClipData.HitboxPrefab;

            clone.AttachBone =
                behitboxClipData.AttachBone;

            clone.LocalOffset =
                behitboxClipData.LocalOffset;

            clone.StartTime =
                behitboxClipData.StartTime;

            clone.Length =
                behitboxClipData.Length;

            clone.EndTime =
                behitboxClipData.EndTime;

            return clone;
        }

        if (source
            is HitboxClipData
                hitboxClipData)
        {
            HitboxClipData clone =
                new HitboxClipData();

            clone.Name =
                source.Name;

            clone.HitboxPrefab =
                hitboxClipData.HitboxPrefab;

            clone.AttachBone =
                hitboxClipData.AttachBone;

            clone.LocalOffset =
                hitboxClipData.LocalOffset;

            clone.StartTime =
                hitboxClipData.StartTime;

            clone.Length =
                hitboxClipData.Length;

            clone.EndTime =
                hitboxClipData.EndTime;

            return clone;
        }

        if (source
            is StateEventData
                stateEventClipData)
        {
            StateEventData clone =
                new StateEventData();

            clone.Name =
                source.Name;

            clone.EventType =
                stateEventClipData.EventType;

            // 重新创建事件实例
            // （[SerializeReference] 对象的深拷贝比较复杂
            // 这里按类型重建一份新的默认实例）
            clone.CreateEventInstance();

            clone.StartTime =
                stateEventClipData.StartTime;

            clone.Length =
                stateEventClipData.Length;

            clone.EndTime =
                stateEventClipData.EndTime;

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

        // Ctrl / Command 多选
        if (additive)
        {
            SelectClip(
                clipData,
                true);

            return;
        }

        // 普通单击：清空之前的选中，只选中当前 clip
        SelectClip(
            clipData,
            false);
    }
}