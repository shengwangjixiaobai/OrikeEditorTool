using System;
using UnityEngine;

[Serializable]
public class VoiceClipData : BaseClipData
{
    public AudioClip Voice;

    public override ClipType Type
    {
        get
        {
            return ClipType.Voice;
        }
    }

    public override void RefreshData()
    {
        if (Voice == null)
        {
            Length = 0f;
            EndTime = StartTime;
            return;
        }

        Length = Voice.length;
        EndTime = StartTime + Length;
    }

    public override BasePreviewData
        GetPreviewDataAtTime(
            float actionTime)
    {
        if (Voice == null)
        {
            return null;
        }

        if (!ContainsTime(actionTime))
        {
            return null;
        }

        float localTime =
            actionTime - StartTime;

        localTime =
            Mathf.Clamp(
                localTime,
                0f,
                Voice.length);

        return new VoicePreviewData(
            Voice,
            localTime);
    }
}