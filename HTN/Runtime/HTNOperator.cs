using System;
using System.Collections.Generic;
using UnityEngine;

namespace Orike.HTN
{
    /// <summary>
    /// 操作函数的单次更新结果。
    /// </summary>
    public enum HTNOperatorStatus
    {
        /// <summary>执行中（下一帧继续）。</summary>
        InProgress,

        /// <summary>成功完成：效果会被施加到真实世界状态，推进到下一个任务。</summary>
        Success,

        /// <summary>失败：计划中止并触发重新规划。</summary>
        Failure,
    }


    /// <summary>
    /// 操作函数的执行上下文（由 HTNBrain 构造并复用）。
    /// </summary>
    public class HTNOperatorContext
    {
        /// <summary>执行计划的 Agent。</summary>
        public HTNBrain Brain;

        /// <summary>当前正在执行的基元任务。</summary>
        public HTNPrimitiveTask Task;

        /// <summary>本次帧间隔（秒）。</summary>
        public float DeltaTime;

        /// <summary>Agent 的 GameObject（快捷访问）。</summary>
        public GameObject GameObject;

        /// <summary>
        /// 操作函数的私有状态暂存位（换任务时会被清空）。
        /// 操作函数实例可能被多个 Agent 共享，
        /// 所以逐 Agent / 逐任务的状态必须放在这里，而不是成员变量里。
        /// </summary>
        public object OperatorState;
    }


    /// <summary>
    /// 操作函数：NPC 可以执行的原于行为（移动、播放动画、开火……）。
    ///
    /// 基元任务 = 操作函数 + 条件 + 效果：同一个操作函数可以被多个任务
    /// 复用，配合不同的条件与效果表达不同的意义（第 12.2.3 节）。
    /// 实现必须是“无逐 Agent 成员状态”的：需要暂存状态时使用
    /// Context.OperatorState。
    /// </summary>
    public abstract class HTNOperator
    {
        /// <summary>
        /// 驱动一次操作函数执行。
        /// 计划执行器每帧对当前任务的操作函数调用一次。
        /// </summary>
        public abstract HTNOperatorStatus Update(HTNOperatorContext context);
    }


    /// <summary>
    /// 委托操作函数：用 lambda 快速组装，适合在 Agent 上注册项目定制行为。
    /// </summary>
    public class FuncHTNOperator : HTNOperator
    {
        private readonly Func<HTNOperatorContext, HTNOperatorStatus> _update;


        public FuncHTNOperator(
            Func<HTNOperatorContext, HTNOperatorStatus> update)
        {
            _update = update;
        }


        public override HTNOperatorStatus Update(HTNOperatorContext context)
        {
            return _update != null
                ? _update(context)
                : HTNOperatorStatus.Failure;
        }
    }


    /// <summary>
    /// 演示用操作函数：只打日志并等待指定秒数，用于不接游戏逻辑时验证整个
    /// 规划 / 执行流程。
    ///
    /// Parameters 格式：“时长(秒)[|日志文本]”，例如 “1.5|冲向敌人”。
    /// </summary>
    public class MockHTNOperator : HTNOperator
    {
        public override HTNOperatorStatus Update(HTNOperatorContext context)
        {
            MockState state =
                context.OperatorState as MockState;

            if (state == null)
            {
                state = ParseParameters(context.Task);

                context.OperatorState = state;

                if (!string.IsNullOrEmpty(state.LogText))
                {
                    Debug.Log(
                        $"[HTN] {context.Brain.name} 开始 {context.Task.name}：{state.LogText}",
                        context.Brain);
                }
            }

            state.Elapsed += context.DeltaTime;

            if (state.Elapsed < state.Duration)
            {
                return HTNOperatorStatus.InProgress;
            }

            if (!string.IsNullOrEmpty(state.LogText))
            {
                Debug.Log(
                    $"[HTN] {context.Brain.name} 完成 {context.Task.name}",
                    context.Brain);
            }

            return HTNOperatorStatus.Success;
        }


        private static MockState ParseParameters(HTNPrimitiveTask task)
        {
            MockState state = new MockState();

            string parameters = task != null ? task.Parameters : null;

            if (!string.IsNullOrEmpty(parameters))
            {
                string[] parts = parameters.Split('|');

                if (parts.Length > 0 &&
                    float.TryParse(
                        parts[0].Trim(),
                        out float duration))
                {
                    state.Duration = Mathf.Max(0f, duration);
                }

                if (parts.Length > 1)
                {
                    state.LogText = parts[1].Trim();
                }
            }

            return state;
        }


        private class MockState
        {
            public float Duration;

            public float Elapsed;

            public string LogText;
        }
    }


    /// <summary>
    /// 操作函数注册表：基元任务通过 OperatorId 字符串引用操作函数，
    /// 运行期由本表解析。
    ///
    /// 默认注册了演示用 Mock；项目自己的操作函数可以在启动时
    /// 调用 Register 注册（建议放在初始化阶段统一注册）。
    /// </summary>
    public static class HTNOperatorRegistry
    {
        private static readonly Dictionary<string, HTNOperator> Operators =
            new Dictionary<string, HTNOperator>();


        static HTNOperatorRegistry()
        {
            Operators["Mock"] = new MockHTNOperator();
        }


        /// <summary>
        /// 注册 / 覆盖一个操作函数。
        /// </summary>
        public static void Register(
            string operatorId,
            HTNOperator instance)
        {
            Operators[operatorId] = instance;
        }


        /// <summary>
        /// 按 ID 取操作函数；未注册返回 null。
        ///
        /// 编辑器下有便捷回退：ID 也可以直接写成 HTNOperator 子类的类名
        /// （通过 TypeCache 查找并自动实例化注册），这样不用手动注册
        /// 就能在编辑器下拉框里选到项目里的操作函数。
        /// 打包构建没有 TypeCache，运行前仍需显式 Register（见 README）。
        /// </summary>
        public static HTNOperator Get(string operatorId)
        {
            if (string.IsNullOrEmpty(operatorId))
            {
                return null;
            }

            if (Operators.TryGetValue(operatorId, out HTNOperator instance))
            {
                return instance;
            }

#if UNITY_EDITOR
            instance =
                CreateByTypeName(operatorId);

            if (instance != null)
            {
                Operators[operatorId] = instance;

                return instance;
            }
#endif

            return null;
        }


#if UNITY_EDITOR
        /// <summary>
        /// 按类名在 TypeCache 中查找 HTNOperator 子类并实例化。
        /// </summary>
        private static HTNOperator CreateByTypeName(string typeName)
        {
            foreach (System.Type type in
                     UnityEditor.TypeCache.GetTypesDerivedFrom<HTNOperator>())
            {
                if (!type.IsClass ||
                    type.IsAbstract)
                {
                    continue;
                }

                if (type.Name == typeName)
                {
                    return (HTNOperator)System.Activator.CreateInstance(type);
                }
            }

            return null;
        }
#endif


        /// <summary>
        /// 所有可用的操作函数 ID（编辑器下拉框使用）：
        /// 已注册的 + 项目里所有 HTNOperator 子类的类名（仅编辑器）。
        /// </summary>
        public static List<string> GetAllIds()
        {
            List<string> ids =
                new List<string>(Operators.Keys);

#if UNITY_EDITOR
            foreach (System.Type type in
                     UnityEditor.TypeCache.GetTypesDerivedFrom<HTNOperator>())
            {
                if (type.IsClass &&
                    !type.IsAbstract &&
                    !ids.Contains(type.Name))
                {
                    ids.Add(type.Name);
                }
            }
#endif

            return ids;
        }
    }
}
