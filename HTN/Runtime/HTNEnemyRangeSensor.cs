using UnityEngine;

namespace Orike.HTN
{
    /// <summary>
    /// 示例感知器：按与目标的距离更新一个“敌人距离档位”属性
    /// （0=近战距离 1=攻击距离 2=远离），演示感知器 → 世界状态 → 重规划
    /// 的完整链路。属性名与阈值都可在 Inspector 配置。
    /// </summary>
    public class HTNEnemyRangeSensor : HTNSensor
    {
        /// <summary>要追踪的目标（如玩家）；为空时不更新。</summary>
        public Transform Target;

        /// <summary>近战距离阈值。</summary>
        public float MeleeRange = 4f;

        /// <summary>攻击距离阈值（大于该值视为“远离”）。</summary>
        public float AttackRange = 12f;

        /// <summary>要写入的世界状态属性名。</summary>
        public string PropertyName = "WsEnemyRange";


        public override bool Sense(
            HTNBrain brain,
            HTNWorldState worldState)
        {
            if (Target == null ||
                !worldState.TryGet(PropertyName, out int current))
            {
                return false;
            }

            float distance =
                Vector3.Distance(
                    Target.position,
                    transform.position);

            int next = distance <= MeleeRange
                ? 0
                : distance <= AttackRange
                    ? 1
                    : 2;

            if (next == current)
            {
                return false;
            }

            worldState.TrySet(
                PropertyName,
                next);

            return true;
        }
    }
}
