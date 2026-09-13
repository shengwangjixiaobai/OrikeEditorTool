/// <summary>
/// 事件类型枚举
///
/// 同时包含持续事件和点事件类型
/// 由 StateEventData / PointEventData 持有
///
/// 新增事件类型时：
///   1. 在此枚举加一项
///   2. 在 EventFactory 中加映射
/// </summary>


public enum EventType
{
    None = 0,

    // =====================================================
    // 持续事件（State Event）
    // =====================================================

    /// <summary>
    /// 调试用：在 OnStart / OnUpdate / OnEnd 打日志
    /// </summary>
    DebugTestState = 1001,


    // =====================================================
    // 点事件（Point Event）
    // =====================================================

    /// <summary>
    /// 调试用：OnCall 时打日志
    /// </summary>
    DebugTestPoint = 2001,
}



