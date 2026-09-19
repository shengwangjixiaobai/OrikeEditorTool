using System;
using UnityEngine;

namespace Orike.HTN
{
    /// <summary>
    /// 条件比较类型。
    /// </summary>
    public enum HTNConditionType
    {
        /// <summary>等于（布尔属性判断 true 即 Equal 1）。</summary>
        Equal,

        /// <summary>不等于。</summary>
        NotEqual,

        /// <summary>大于。</summary>
        Greater,

        /// <summary>大于等于。</summary>
        GreaterOrEqual,

        /// <summary>小于。</summary>
        Less,

        /// <summary>小于等于。</summary>
        LessOrEqual,
    }


    /// <summary>
    /// 条件：对某个世界状态属性的比较断言。
    ///
    /// 同时用于：
    ///   - 实现方法（HTNMethod）的预设条件：决定该方法是否可以被选中；
    ///   - 基元任务（HTNPrimitiveTask）的执行条件：决定该任务能否进入计划。
    /// </summary>
    [Serializable]
    public class HTNCondition
    {
        /// <summary>世界状态属性名。</summary>
        public string Property = "WsCanSeeEnemy";

        /// <summary>比较类型。</summary>
        public HTNConditionType Type = HTNConditionType.Equal;

        /// <summary>比较值（布尔属性使用 0 / 1）。</summary>
        public int Value;


        /// <summary>
        /// 在给定世界状态下判断条件是否成立。
        /// 属性不存在视为不成立（并给出警告，避免域配置错误被静默吞掉）。
        /// </summary>
        public bool Evaluate(HTNWorldState worldState)
        {
            if (!worldState.TryGet(Property, out int actual))
            {
                Debug.LogWarning(
                    $"[HTN] 条件引用了未知的世界状态属性：{Property}");

                return false;
            }

            return Type switch
            {
                HTNConditionType.Equal => actual == Value,
                HTNConditionType.NotEqual => actual != Value,
                HTNConditionType.Greater => actual > Value,
                HTNConditionType.GreaterOrEqual => actual >= Value,
                HTNConditionType.Less => actual < Value,
                HTNConditionType.LessOrEqual => actual <= Value,
                _ => false,
            };
        }


        /// <summary>条件摘要（编辑器节点 / 计划日志显示）。</summary>
        public string Describe()
        {
            string op = Type switch
            {
                HTNConditionType.Equal => "==",
                HTNConditionType.NotEqual => "!=",
                HTNConditionType.Greater => ">",
                HTNConditionType.GreaterOrEqual => ">=",
                HTNConditionType.Less => "<",
                HTNConditionType.LessOrEqual => "<=",
                _ => "?",
            };

            return $"{Property} {op} {Value}";
        }
    }
}
