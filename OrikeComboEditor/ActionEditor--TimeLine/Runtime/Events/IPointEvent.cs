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
    void OnCall();
}
