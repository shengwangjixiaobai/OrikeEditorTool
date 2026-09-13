using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TrackData
{
    public string TrackName;

    public ClipType ClipType =
        ClipType.Animation;

    [SerializeReference]
    public List<BaseClipData> Clips =
        new List<BaseClipData>();

    /// <summary>
    /// 点事件列表
    ///
    /// 点事件可以附加到任意轨道上
    /// 显示为时间点标记，不是 Clip 片段
    /// </summary>
    [SerializeReference]
    public List<PointEventData> PointEvents =
        new List<PointEventData>();

    public bool CanAcceptClip(
        ClipType clipType)
    {
        return ClipType == clipType;
    }

    public bool CanAcceptClip(
        BaseClipData clip)
    {
        if (clip == null)
        {
            return false;
        }

        return CanAcceptClip(
            clip.Type);
    }

    public void RefreshData()
    {
        if (Clips == null)
        {
            Clips =
                new List<BaseClipData>();
        }

        if (PointEvents == null)
        {
            PointEvents =
                new List<PointEventData>();
        }

        foreach (
            BaseClipData clip
            in Clips)
        {
            if (clip == null)
            {
                continue;
            }

            clip.RefreshData();
        }
    }
}