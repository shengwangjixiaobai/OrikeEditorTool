using System;
using System.Collections.Generic;

/// <summary>
/// 事件工厂
///
/// 根据 EventType 枚举创建对应的事件实例
/// 同时提供：
///   - 是否持续事件 / 点事件的判断
///   - 获取所有可用类型的列表（供 Inspector 下拉）
///
/// 新增事件类型时，只需在此加映射
/// </summary>
public static class EventFactory
{
    // =========================================================
    // 持续事件映射
    // =========================================================

    private static readonly Dictionary<
        EventType,
        Func<IStateEvent>> _stateEventCreators =
        new Dictionary<
            EventType,
            Func<IStateEvent>>()
        {
            {
                EventType.DebugTestState,
                () => new DebugTestStateEvent()
            },
        };


    // =========================================================
    // 点事件映射
    // =========================================================

    private static readonly Dictionary<
        EventType,
        Func<IPointEvent>> _pointEventCreators =
        new Dictionary<
            EventType,
            Func<IPointEvent>>()
        {
            {
                EventType.DebugTestPoint,
                () => new DebugTestPointEvent()
            },
        };


    // =========================================================
    // 创建持续事件
    // =========================================================

    public static IStateEvent CreateStateEvent(
        EventType eventType)
    {
        if (eventType == EventType.None)
        {
            return null;
        }

        if (_stateEventCreators.TryGetValue(
                eventType,
                out var creator))
        {
            return creator();
        }

        return null;
    }


    // =========================================================
    // 创建点事件
    // =========================================================

    public static IPointEvent CreatePointEvent(
        EventType eventType)
    {
        if (eventType == EventType.None)
        {
            return null;
        }

        if (_pointEventCreators.TryGetValue(
                eventType,
                out var creator))
        {
            return creator();
        }

        return null;
    }


    // =========================================================
    // 是否持续事件类型
    // =========================================================

    public static bool IsStateEventType(
        EventType eventType)
    {
        return _stateEventCreators
            .ContainsKey(eventType);
    }


    // =========================================================
    // 是否点事件类型
    // =========================================================

    public static bool IsPointEventType(
        EventType eventType)
    {
        return _pointEventCreators
            .ContainsKey(eventType);
    }


    // =========================================================
    // 获取所有持续事件类型
    // =========================================================

    public static EventType[] GetStateEventTypes()
    {
        EventType[] result =
            new EventType[_stateEventCreators.Count];

        int i = 0;

        foreach (
            EventType type
            in _stateEventCreators.Keys)
        {
            result[i++] = type;
        }

        return result;
    }


    // =========================================================
    // 获取所有点事件类型
    // =========================================================

    public static EventType[] GetPointEventTypes()
    {
        EventType[] result =
            new EventType[_pointEventCreators.Count];

        int i = 0;

        foreach (
            EventType type
            in _pointEventCreators.Keys)
        {
            result[i++] = type;
        }

        return result;
    }
}
