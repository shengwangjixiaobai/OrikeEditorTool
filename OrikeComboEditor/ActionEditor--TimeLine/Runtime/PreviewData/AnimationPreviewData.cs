using System;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public class AnimationPreviewData : BasePreviewData
{
    /// <summary>
    /// 当前需要预览的动画
    /// </summary>
    public AnimationClip Animation;

    /// <summary>
    /// 当前动画内部时间
    /// </summary>
    public float LocalTime;

    public AnimationPreviewData(
        AnimationClip animation,
        float localTime)
    {
        Animation = animation;
        LocalTime = localTime;
    }
}