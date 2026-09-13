using System;
using UnityEngine;

/// <summary>
/// 持续事件 Clip（状态事件）
///
/// 在 Clip 时间段内持续触发事件
///   - 进入时间段：IStateEvent.OnStart()
///   - 时间段内每帧：IStateEvent.OnUpdate(delta)
///   - 离开时间段：IStateEvent.OnEnd()
///
/// StateEvent 持有 EventType 和具体的 IStateEvent 实例
/// Inspector 中选择 EventType 后，通过 EventFactory
/// 创建对应事件实例，并绘制其字段
/// </summary>
[Serializable]
public class StateEventData
    : BaseClipData
{
    public EventType EventType;

    /// <summary>
    /// 具体的持续事件实例
    /// [SerializeReference] 支持多态序列化
    /// </summary>
    [SerializeReference]
    public IStateEvent StateEvent;


    public override ClipType Type
    {
        get
        {
            return ClipType.StateEvent;
        }
    }


    public override void RefreshData()
    {
        if (Length <= 0f)
        {
            Length = 1f;
        }

        EndTime =
            StartTime +
            Length;
    }


    /// <summary>
    /// 持续事件不需要预览数据
    /// 由 ActionPreviewSystem 直接调用事件方法
    /// </summary>
    public override BasePreviewData
        GetPreviewDataAtTime(
            float actionTime)
    {
        return null;
    }


    /// <summary>
    /// 根据 EventType 创建事件实例
    /// 由 Inspector / Controller 调用
    /// </summary>
    public void CreateEventInstance()
    {
        StateEvent =
            EventFactory.CreateStateEvent(
                EventType);
    }
}
