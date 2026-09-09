using System;
using UnityEngine;

/// <summary>
/// 点事件
///
/// 在某个时间点触发一次
///   - 当 Playhead 穿越 Time 时调用 IPointEvent.OnCall()
///
/// 点事件可以附加到任意轨道上
/// 显示为一个标记（菱形），不是 Clip 片段
/// </summary>
[Serializable]
public class PointEventData
{
    /// <summary>
    /// 显示名称
    /// </summary>
    public string Name = "Point Event";

    /// <summary>
    /// 触发时间点（秒）
    /// </summary>
    public float Time;

    public EventType EventType;

    /// <summary>
    /// 具体的点事件实例
    /// [SerializeReference] 支持多态序列化
    /// </summary>
    [SerializeReference]
    public IPointEvent PointEvent;


    /// <summary>
    /// 根据 EventType 创建事件实例
    /// </summary>
    public void CreateEventInstance()
    {
        PointEvent =
            EventFactory.CreatePointEvent(
                EventType);
    }
}
