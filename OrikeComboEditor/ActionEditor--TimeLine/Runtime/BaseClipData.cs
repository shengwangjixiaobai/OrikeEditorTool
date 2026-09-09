
using System;
using UnityEngine;

[Serializable]
public abstract class BaseClipData
{
    public float StartTime;

    public float EndTime;

    public float Length;

    public string Name;


    /// <summary>
    /// 当前 Clip 的类型
    /// </summary>
    public abstract ClipType Type
    {
        get;
    }


    /// <summary>
    /// 刷新 Clip 数据
    ///
    /// 当 Clip 内部资源发生变化时调用
    /// </summary>
    public virtual void RefreshData()
    {
    }


    /// <summary>
    /// 判断指定时间是否位于当前 Clip 内
    /// </summary>
    public bool ContainsTime(
        float actionTime)
    {
        return
            actionTime >= StartTime &&
            actionTime <= EndTime;
    }


    /// <summary>
    /// 获取指定时间的预览数据
    /// </summary>
    public abstract BasePreviewData
        GetPreviewDataAtTime(
            float actionTime);
}
