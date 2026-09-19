using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Orike.HTN
{
    /// <summary>
    /// 基元任务：可以由 NPC 执行的一个步骤，计划的组成单位。
    ///
    /// 由三部分组成（第 12.2.3 节）：
    ///   - 操作函数（OperatorId + Parameters）：NPC 可执行的原于行为；
    ///     同一个操作函数可以被多个任务复用（如 MoveTo 同时服务
    ///     “冲刺到敌人” 与 “走向下一座桥”）；
    ///   - 条件（Conditions）：全部成立时任务才允许进入计划；
    ///   - 效果（Effects）：任务成功执行对世界状态的影响，
    ///     其中 Expected 标记的为“期望效果”，只在规划 / 校验阶段生效。
    ///
    /// 注意：任务是所属定义域的子资源，由 HTNDomainAssetOps 程序化创建，
    /// 不提供 CreateAssetMenu（避免产生不属于任何域的孤儿任务资产）。
    /// </summary>
    public class HTNPrimitiveTask : HTNTask
    {
        /// <summary>操作函数 ID（由 HTNOperatorRegistry 注册 / 解析）。</summary>
        public string OperatorId = "Mock";

        /// <summary>传给操作函数的参数（格式由操作函数自行约定）。</summary>
        [TextArea]
        public string Parameters;


        /// <summary>执行条件：全部成立时该任务才允许进入计划。</summary>
        public List<HTNCondition> Conditions =
            new List<HTNCondition>();

        /// <summary>效果：成功执行后施加到世界状态（含期望效果）。</summary>
        public List<HTNEffect> Effects =
            new List<HTNEffect>();


        /// <summary>判断任务条件在给定世界状态下是否全部成立。</summary>
        public bool AreConditionsSatisfied(HTNWorldState worldState)
        {
            foreach (HTNCondition condition in Conditions)
            {
                if (condition == null ||
                    !condition.Evaluate(worldState))
                {
                    return false;
                }
            }

            return true;
        }


        /// <summary>
        /// 把普通效果施加到世界状态（任务执行成功后调用）。
        /// 期望效果只服务于规划阶段，不在此施加。
        /// </summary>
        public void ApplyEffects(HTNWorldState worldState)
        {
            ApplyEffects(worldState, false);
        }


        /// <summary>
        /// 把效果施加到世界状态。
        /// </summary>
        /// <param name="worldState">目标世界状态。</param>
        /// <param name="includeExpected">
        /// 是否包含期望效果：规划 / 计划校验时为 true，实际执行成功后为 false。
        /// </param>
        public void ApplyEffects(
            HTNWorldState worldState,
            bool includeExpected)
        {
            foreach (HTNEffect effect in Effects)
            {
                if (effect == null ||
                    (effect.Expected && !includeExpected))
                {
                    continue;
                }

                effect.Apply(worldState);
            }
        }


        /// <summary>条件摘要（节点显示）。</summary>
        public string DescribeConditions()
        {
            if (Conditions == null ||
                Conditions.Count == 0)
            {
                return "无条件";
            }

            StringBuilder sb = new StringBuilder();

            foreach (HTNCondition condition in Conditions)
            {
                if (condition == null)
                {
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.Append(" && ");
                }

                sb.Append(condition.Describe());
            }

            return sb.ToString();
        }


        /// <summary>效果摘要（节点显示）。</summary>
        public string DescribeEffects()
        {
            if (Effects == null ||
                Effects.Count == 0)
            {
                return "无效果";
            }

            StringBuilder sb = new StringBuilder();

            foreach (HTNEffect effect in Effects)
            {
                if (effect == null)
                {
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }

                sb.Append(effect.Describe());
            }

            return sb.ToString();
        }
    }
}
