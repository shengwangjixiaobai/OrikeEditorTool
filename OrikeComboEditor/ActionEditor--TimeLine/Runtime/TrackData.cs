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