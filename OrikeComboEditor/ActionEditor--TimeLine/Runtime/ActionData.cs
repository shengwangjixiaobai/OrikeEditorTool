using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ActionData",
    menuName = "Orike/Action Data")]
public class ActionData : ScriptableObject
{
    public List<TrackData> Tracks =
        new List<TrackData>();

    public void RefreshData()
    {
        if (Tracks == null)
        {
            Tracks =
                new List<TrackData>();
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