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

        // 仅在 Length 尚未初始化时用音频全长为默认值；
        // 已被裁剪过后要保留用户设置的 Length，避免刷新/加载时丢失裁剪。
        if (Length <= 0f)
        {
            Length =
                Voice.length;
        }

        EndTime =
            StartTime +
            Length;
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