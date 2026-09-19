using System;
using UnityEngine;

[Serializable]
public class AnimationClipData
    : BaseClipData
{
    public AnimationClip Animation;


    /// <summary>
    /// 当前 Clip 类型
    /// </summary>
    public override ClipType Type
    {
        get
        {
            return ClipType.Animation;
        }
    }


    /// <summary>
    /// 刷新 Animation 数据
    /// </summary>
    public override void RefreshData()
    {
        if (Animation == null)
        {
            Length = 0f;

            EndTime = StartTime;

            return;
        }

        // 仅在 Length 尚未初始化时用动画全长为默认值；
        // 已被裁剪过后要保留用户设置的 Length，避免刷新/加载时丢失裁剪。
        if (Length <= 0f)
        {
            Length =
                Animation.length;
        }

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