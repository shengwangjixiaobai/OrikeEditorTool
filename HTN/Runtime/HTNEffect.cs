using System;
using UnityEngine;

namespace Orike.HTN
{
    /// <summary>
    /// 效果作用方式。
    /// </summary>
    public enum HTNEffectOp
    {
        /// <summary>设置为指定值。</summary>
        Set,

        /// <summary>加。</summary>
        Add,

        /// <summary>减。</summary>
        Subtract,

        /// <summary>置 1（布尔属性专用语法糖）。</summary>
        SetTrue,

        /// <summary>置 0（布尔属性专用语法糖）。</summary>
        SetFalse,
    }


    /// <summary>
    /// 效果：任务成功执行后对世界状态的影响。
    ///
    /// 第 12 章区分了两种效果：
    ///   - 普通效果：规划时施加到工作世界状态（模拟任务一定成功），
    ///               执行成功后再施加到真实世界状态；
    ///   - 期望效果（Expected）：只在规划与计划校验阶段施加。
    ///     用于描述“执行过程中世界状态理应发生的变化”，例如导航到敌人附近后
    ///     视觉感知器理应把 WsCanSeeEnemy 置 1 —— 感知器的变化任务本身不会
    ///     直接产生，但没有这条期望效果，后续依赖它的任务在规划时就无法通过校验。
    /// </summary>
    [Serializable]
    public class HTNEffect
    {
        /// <summary>世界状态属性名。</summary>
        public string Property = "WsLocation";

        /// <summary>作用方式。</summary>
        public HTNEffectOp Op = HTNEffectOp.Set;

        /// <summary>作用值（SetTrue / SetFalse 时忽略）。</summary>
        public int Value;

        /// <summary>是否为期望效果（仅规划 / 校验阶段生效）。</summary>
        public bool Expected;


        /// <summary>把效果施加到给定世界状态上。</summary>
        public void Apply(HTNWorldState worldState)
        {
            if (!worldState.TryGet(Property, out int current))
            {
                Debug.LogWarning(
                    $"[HTN] 效果引用了未知的世界状态属性：{Property}");

                return;
            }

            int next = Op switch
            {
                HTNEffectOp.Set => Value,
                HTNEffectOp.Add => current + Value,
                HTNEffectOp.Subtract => current - Value,
                HTNEffectOp.SetTrue => 1,
                HTNEffectOp.SetFalse => 0,
                _ => current,
            };

            worldState.TrySet(
                Property,
                next);
        }


        /// <summary>效果摘要（编辑器节点 / 计划日志显示）。</summary>
        public string Describe()
        {
            string body = Op switch
            {
                HTNEffectOp.Set => $"{Property} = {Value}",
                HTNEffectOp.Add => $"{Property} += {Value}",
                HTNEffectOp.Subtract => $"{Property} -= {Value}",
                HTNEffectOp.SetTrue => $"{Property} = true",
                HTNEffectOp.SetFalse => $"{Property} = false",
                _ => Property,
            };

            return Expected
                ? $"{body}（期望）"
                : body;
        }
    }
}
