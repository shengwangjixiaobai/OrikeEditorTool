using System;
using UnityEngine;

/// <summary>
/// 调试用点事件
///
/// OnCall 时打一条日志
/// </summary>
[Serializable]
public class DebugTestPointEvent
    : IPointEvent
{
    [SerializeField]
    private string _message = "Debug Point Event";


    public void OnCall()
    {
        Debug.Log(
            $"[PointEvent] OnCall: {_message}");
    }
}
