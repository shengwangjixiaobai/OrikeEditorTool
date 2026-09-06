using System;
using System.Collections.Generic;
using UnityEngine;
[Serializable]
public class TrackData
{
    public string TrackName;

    [SerializeReference]
    public List<BaseClipData> Clips =
        new List<BaseClipData>();


    public void RefreshData()
    {
        if (Clips == null)
        {
            return;
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