using System.Collections.Generic;
using UnityEngine;

public class ActionData
    : ScriptableObject
{
    public List<TrackData> Tracks =
        new List<TrackData>();


    public void RefreshData()
    {
        if (Tracks == null)
        {
            return;
        }

        foreach (
            TrackData track
            in Tracks)
        {
            if (track == null)
            {
                continue;
            }

            track.RefreshData();
        }
    }
}
