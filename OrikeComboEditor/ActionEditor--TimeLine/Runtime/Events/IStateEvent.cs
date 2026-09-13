/// <summary>
/// 持续事件（状态事件）接口
///
/// 在 Clip 时间段内持续生效
///   OnStart  - 进入时间段时调用一次
///   OnUpdate - 时间段内每帧调用
///   OnEnd    - 离开时间段时调用一次
///
/// 实现类需为 [Serializable]，由 StateEventData 通过
/// [SerializeReference] 持有，支持多态序列化
/// </summary>
public interface IStateEvent
{

    void OnStart();

    void OnUpdate(
        float deltaTime);

    void OnEnd();
}
