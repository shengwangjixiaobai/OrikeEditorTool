using System;
using UnityEngine;

[Serializable]
public class AnimationClipData
    : BaseClipData
{
    public AnimationClip Animation;


    /// <summary>
    /// Ë¢ÐÂ Animation Êý¾Ý
    /// </summary>
    public override void RefreshData()
    {
        if (Animation == null)
        {
            Length = 0f;

            EndTime = StartTime;

            return;
        }

        Length =
            Animation.length;

        EndTime =
            StartTime +
            Length;
    }


    public override BasePreviewData
        GetPreviewDataAtTime(
            float actionTime)
    {
        if (Animation == null)
        {
            return null;
        }

        if (!ContainsTime(actionTime))
        {
            return null;
        }

        float localTime =
            actionTime -
            StartTime;

        localTime =
            Mathf.Clamp(
                localTime,
                0f,
                Animation.length);

        return new AnimationPreviewData(
            Animation,
            localTime);
    }
}