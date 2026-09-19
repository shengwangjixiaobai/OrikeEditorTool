using UnityEngine;

namespace Orike.HTN
{
    /// <summary>
    /// 调试感知器：按固定间隔把一个效果反复施加到世界状态，
    /// 用于在没有真实游戏逻辑时测试“世界变化 → 重新规划”链路。
    ///
    /// 用法：挂在 HTNBrain 同一物体或其子物体上，
    /// 配置 Effect（属性 / 作用方式 / 值）与 Interval（秒）。
    /// 例如每 2 秒翻转 WsCanSeeEnemy，就能在 Play 模式看到角色
    /// 在“攻击敌人”与“巡逻”之间来回切换（配合 VerboseLog 更直观）。
    ///
    /// 注意：只有施加后属性值真的发生了变化才返回 true，
    /// 值没变（如反复置 1）不会触发无意义的重规划。
    /// </summary>
    public class HTNDebugSensor : HTNSensor
    {
        /// <summary>施加的效果。</summary>
        public HTNEffect Effect = new HTNEffect
        {
            Property = "WsCanSeeEnemy",
            Op = HTNEffectOp.Set,
            Value = 1,
        };

        /// <summary>施加间隔（秒），<= 0 表示不生效。</summary>
        public float Interval = 2f;

        private float _timer;


        public override bool Sense(
            HTNBrain brain,
            HTNWorldState worldState)
        {
            if (Effect == null ||
                Interval <= 0f)
            {
                return false;
            }

            _timer += Time.deltaTime;

            if (_timer < Interval)
            {
                return false;
            }

            _timer = 0f;

            worldState.TryGet(Effect.Property, out int before);

            Effect.Apply(worldState);

            worldState.TryGet(Effect.Property, out int after);

            return before != after;
        }
    }
}
