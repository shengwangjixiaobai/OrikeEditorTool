using System;

/// <summary>
/// 事件工厂（兼容门面）
///
/// 内部委托 EventRegistry 自动扫描实现。
/// 新代码请直接使用 EventRegistry。
/// </summary>
public static class EventFactory
{
    // =========================================================
    // 创建持续事件（按类型名）
    // =========================================================

    public static IStateEvent CreateStateEvent(
        string typeName)
    {
        return EventRegistry.CreateStateEvent(
            typeName);
    }


    // =========================================================
    // 创建点事件（按类型名）
    // =========================================================

    public static IPointEvent CreatePointEvent(
        string typeName)
    {
        return EventRegistry.CreatePointEvent(
            typeName);
    }


    // =========================================================
    // 是否持续事件类型
    // =========================================================

    public static bool IsStateEventType(
        string typeName)
    {
        return EventRegistry.IsStateEventType(
            typeName);
    }


    // =========================================================
    // 是否点事件类型
    // =========================================================

    public static bool IsPointEventType(
        string typeName)
    {
        return EventRegistry.IsPointEventType(
            typeName);
    }


    // =========================================================
    // 获取所有持续事件类型名
    // =========================================================

    public static string[] GetStateEventTypeNames()
    {
        return EventRegistry
            .GetStateEventTypeNames();
    }


    // =========================================================
    // 获取所有点事件类型名
    // =========================================================

    public static string[] GetPointEventTypeNames()
    {
        return EventRegistry
            .GetPointEventTypeNames();
    }
}
