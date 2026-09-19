using UnityEngine;

/// <summary>
/// 点事件接口
///
/// 在某个时间点触发一次
///   OnCall - 当 Playhead 穿越该时间点时调用一次
///
/// 实现类需为 [Serializable]，由 PointEventData 通过
/// [SerializeReference] 持有，支持多态序列化
/// </summary>
public interface IPointEvent
{
    /// <summary>
    /// 在触发点调用一次。
    /// owner 为播放该动作的角色根物体（ActionPlayer 的 MasterObject），
    /// 用于按名字解析场景中的物体。
    /// </summary>
    void OnCall(
        GameObject owner);
}