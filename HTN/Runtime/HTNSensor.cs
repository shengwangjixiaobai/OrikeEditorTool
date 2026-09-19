using UnityEngine;

namespace Orike.HTN
{
    /// <summary>
    /// 感知器：把游戏世界的变化编码为 HTN 能理解的世界状态。
    ///
    /// 第 12.2.2 节：规划器无法感知任务之外的世界变化（敌人移动、生命
    /// 变化等），这类信息统一由感知器写入世界状态；感知器更新导致
    /// 世界状态变化时会通知 Brain 触发重新规划。
    ///
    /// 挂在 HTNBrain 同一物体或其子物体上，由 Brain 自动收集。
    /// 具体实现见 HTNEnemyRangeSensor / HTNDebugSensor，
    /// 项目自定义感知器继承本类并实现 Sense 即可（见 README 第 3 节）。
    /// </summary>
    public abstract class HTNSensor : MonoBehaviour
    {
        /// <summary>
        /// 采样一次并更新世界状态。
        /// </summary>
        /// <returns>本次采样是否改变了世界状态（决定是否触发重新规划）。</returns>
        public abstract bool Sense(
            HTNBrain brain,
            HTNWorldState worldState);
    }
}
