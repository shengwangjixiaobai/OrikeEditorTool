using System;
using UnityEngine;

/// <summary>
/// 调试用持续事件
///
/// OnStart / OnEnd 打一条日志
/// OnUpdate 按间隔打日志，避免刷屏
/// </summary>
[Serializable]
public class DebugTestStateEvent
    : IStateEvent
{
    [SerializeField]
    private string _message = "Debug State Event";

    [SerializeField]
    private float _logInterval = 0.5f;

    private float _timer;


    public void OnStart()
    {
        Debug.Log(
            $"[StateEvent] OnStart: {_message}");

        _timer = 0f;
    }


    public void OnUpdate(
        float deltaTime)
    {
        _timer += deltaTime;

        if (_timer >= _logInterval)
        {
            _timer = 0f;

            Debug.Log(
                $"[StateEvent] OnUpdate: {_message}");
        }
    }


    public void OnEnd()
    {
        Debug.Log(
            $"[StateEvent] OnEnd: {_message}");
    }
}
