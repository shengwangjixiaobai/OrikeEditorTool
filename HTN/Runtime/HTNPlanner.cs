using System.Collections.Generic;
using System.Text;

namespace Orike.HTN
{
    /// <summary>
    /// 规划结果：一组基元任务（最终计划）+ 方法遍历记录（MTR）。
    /// </summary>
    public class HTNPlan
    {
        /// <summary>最终计划：按执行顺序排列的基元任务。</summary>
        public readonly List<HTNPrimitiveTask> Tasks =
            new List<HTNPrimitiveTask>();

        /// <summary>
        /// 方法遍历记录（MTR）：按分解顺序记录每个复合任务选中的方法索引。
        /// 用于比较新旧计划的优先级（第 12.8 节）。
        /// </summary>
        public readonly List<HTNMtrEntry> Mtr =
            new List<HTNMtrEntry>();

        /// <summary>规划是否成功（得到至少一个可执行计划）。</summary>
        public bool Success;

        /// <summary>失败原因（Success 为 false 时可读）。</summary>
        public string FailReason;


        /// <summary>计划摘要（日志显示，如 A → B → C）。</summary>
        public string Describe()
        {
            if (Tasks.Count == 0)
            {
                return "（空计划）";
            }

            StringBuilder sb = new StringBuilder();

            foreach (HTNPrimitiveTask task in Tasks)
            {
                if (sb.Length > 0)
                {
                    sb.Append(" → ");
                }

                sb.Append(task.name);
            }

            return sb.ToString();
        }
    }


    /// <summary>
    /// MTR 条目：某复合任务在某次分解中选中的方法索引。
    /// </summary>
    public struct HTNMtrEntry
    {
        /// <summary>被分解的复合任务。</summary>
        public HTNCompoundTask CompoundTask;

        /// <summary>选中的实现方法索引（越小优先级越高）。</summary>
        public int MethodIndex;
    }


    /// <summary>
    /// 分解日志条目：记录规划过程的每一步，供编辑器“规划测试”面板展示。
    /// </summary>
    public class HTNPlanLogEntry
    {
        public enum Kind
        {
            /// <summary>复合任务分解：选中某个实现方法。</summary>
            Decompose,

            /// <summary>复合任务所有方法都不满足条件。</summary>
            CompoundFailed,

            /// <summary>基元任务加入最终计划。</summary>
            PrimitiveAccepted,

            /// <summary>基元任务条件不满足被拒绝。</summary>
            PrimitiveRejected,

            /// <summary>回溯到上一次分解。</summary>
            Backtrack,
        }


        public Kind EntryKind;

        /// <summary>相关任务（复合任务或基元任务）。</summary>
        public HTNTask Task;

        /// <summary>选中 / 被拒的方法索引。</summary>
        public int MethodIndex;

        public string Message;


        public override string ToString()
        {
            return Message;
        }
    }


    /// <summary>
    /// HTN 规划器：全序正向分解（total-order forward decomposition）。
    ///
    /// 算法（第 12.4 节伪代码）：
    ///   1. 根任务压入 TasksToProcess 栈，复制一份世界状态作为
    ///      “工作世界状态”，用于模拟任务执行后的未来；
    ///   2. 每轮弹出一个任务：
    ///      - 复合任务：按优先级顺序搜索实现方法，选中第一个条件满足的，
    ///        把其子任务压回栈；一个都找不到则回溯；
    ///      - 基元任务：条件在工作世界状态下满足则加入最终计划，
    ///        并把效果施加到工作世界状态（假定任务一定成功）；
    ///        不满足则回溯；
    ///   3. 回溯通过“分解历史栈”（DecompHistory）实现：记录每次分解时
    ///      TasksToProcess / FinalPlan / MTR 的快照以及被选中的方法，
    ///      还原时把该复合任务重新压回栈并从下一个方法继续尝试；
    ///   4. 栈为空时规划结束：得到最终计划，或整体失败。
    ///
    /// 另外支持第 12.8 节的 MTR 优先级约束：传入当前运行中计划的 MTR，
    /// 规划时复合任务只允许选择“优先级相同或更高”的方法（索引 <= 记录值），
    /// 从而保证重规划不会打断仍然合法的低优先级行为。
    /// </summary>
    public class HTNPlanner
    {
        /// <summary>
        /// 分解历史条目：一次复合任务分解的完整快照。
        /// </summary>
        private class DecompositionHistory
        {
            /// <summary>发生分解的复合任务。</summary>
            public HTNCompoundTask CompoundTask;

            /// <summary>本次分解选中的方法索引（回溯后从它的下一个方法继续）。</summary>
            public int TriedMethodIndex;

            /// <summary>弹出该复合任务之后、压入子任务之前的待处理任务栈快照。</summary>
            public List<HTNTask> TasksSnapshot;

            /// <summary>快照时的最终计划长度。</summary>
            public int PlanCount;

            /// <summary>快照时的 MTR 长度。</summary>
            public int MtrCount;

            /// <summary>
            /// 快照时的工作世界状态。
            /// 回溯必须连状态一起还原：被丢弃分支里基元任务施加的效果
            /// 不能污染兄弟分支（以及后续方法重试）的分解决策。
            /// </summary>
            public HTNWorldState WorkingStateSnapshot;
        }


        /// <summary>防死循环：递归域写错时的安全上限。</summary>
        private const int MaxIterations = 20000;


        // =========================================================
        // 规划入口
        // =========================================================

        /// <summary>
        /// 从根任务开始规划。
        /// </summary>
        /// <param name="domain">任务所在的定义域（提供世界状态定义）。</param>
        /// <param name="worldState">当前世界状态（规划器内部会复制，不修改原状态）。</param>
        /// <param name="log">
        /// 可选的日志列表：传入非 null 时，分解过程的每一步都会追加到这里。
        /// </param>
        /// <param name="lastPlan">
        /// 当前运行中的计划（可选）。提供时启用 MTR 优先级约束：
        /// 新计划对每个复合任务只能选择优先级相同或更高的方法。
        /// </param>
        public HTNPlan Plan(
            HTNDomain domain,
            HTNWorldState worldState,
            List<HTNPlanLogEntry> log = null,
            HTNPlan lastPlan = null)
        {
            HTNPlan plan = new HTNPlan();

            if (domain == null)
            {
                plan.FailReason = "未指定 HTN 定义域";

                return plan;
            }

            if (domain.RootTask == null)
            {
                plan.FailReason = "定义域没有设置根任务";

                return plan;
            }

            if (!(domain.RootTask is HTNCompoundTask rootCompound))
            {
                plan.FailReason = $"根任务 {domain.RootTask.name} 不是复合任务";

                return plan;
            }

            if (worldState == null)
            {
                plan.FailReason = "未提供世界状态";

                return plan;
            }

            // 第 12.8 节：把当前计划的 MTR 转成约束表。
            // 同一复合任务可能递归出现多次，这里取其中最小的方法索引
            // （最严格的约束），保证新计划任何一处都不会选到更低优先级的方法。
            Dictionary<HTNCompoundTask, int> mtrLimit =
                BuildMtrLimit(lastPlan);

            // ---- 初始化 ----
            // 待处理任务栈（List 的末尾作为栈顶）
            List<HTNTask> tasksToProcess =
                new List<HTNTask> { rootCompound };

            // 工作世界状态：模拟任务效果，不影响调用方的真实状态
            HTNWorldState workingState = worldState.Clone();

            List<HTNPrimitiveTask> finalPlan = plan.Tasks;
            List<HTNMtrEntry> mtr = plan.Mtr;

            Stack<DecompositionHistory> history =
                new Stack<DecompositionHistory>();

            // 回溯后重试某复合任务时的起始方法索引（单槽：
            // 回溯立即把复合任务压回栈顶，下一次弹出它的就是本次重试）
            int retryMethodIndex = 0;
            HTNCompoundTask retryTarget = null;

            int iteration = 0;

            // ---- 主循环 ----
            while (tasksToProcess.Count > 0)
            {
                if (++iteration > MaxIterations)
                {
                    plan.FailReason =
                        $"分解迭代超过上限 {MaxIterations}，" +
                        "请检查域中是否存在无法满足终止条件的递归";

                    return plan;
                }

                HTNTask task =
                    tasksToProcess[tasksToProcess.Count - 1];

                tasksToProcess.RemoveAt(tasksToProcess.Count - 1);

                if (task == null)
                {
                    continue;
                }


                // ---- 复合任务：分解 ----
                if (task is HTNCompoundTask compound)
                {
                    int startIndex = 0;

                    // 是回溯目标本身 → 从上次失败方法的下一个继续
                    if (compound == retryTarget)
                    {
                        startIndex = retryMethodIndex;
                    }

                    int selectedMethodIndex = -1;
                    HTNMethod selectedMethod = null;

                    if (compound.Methods != null)
                    {
                        for (int i = startIndex; i < compound.Methods.Count; i++)
                        {
                            HTNMethod method = compound.Methods[i];

                            if (method == null)
                            {
                                continue;
                            }

                            // MTR 优先级约束：不允许选择比当前运行计划
                            // 更低优先级（索引更大）的方法
                            if (mtrLimit != null &&
                                mtrLimit.TryGetValue(compound, out int limit) &&
                                i > limit)
                            {
                                break;
                            }

                            if (method.IsSatisfied(workingState))
                            {
                                selectedMethodIndex = i;
                                selectedMethod = method;

                                break;
                            }
                        }
                    }


                    // 找到可用方法：记录历史 → 压入子任务
                    if (selectedMethod != null)
                    {
                    history.Push(new DecompositionHistory
                    {
                        CompoundTask = compound,
                        TriedMethodIndex = selectedMethodIndex,
                        TasksSnapshot = new List<HTNTask>(tasksToProcess),
                        PlanCount = finalPlan.Count,
                        MtrCount = mtr.Count,
                        WorkingStateSnapshot = workingState.Clone(),
                    });

                        mtr.Add(new HTNMtrEntry
                        {
                            CompoundTask = compound,
                            MethodIndex = selectedMethodIndex,
                        });

                        AddLog(
                            log,
                            HTNPlanLogEntry.Kind.Decompose,
                            compound,
                            $"{compound.name} 分解为 方法{selectedMethodIndex}" +
                            (string.IsNullOrEmpty(selectedMethod.MethodName)
                                ? ""
                                : $"（{selectedMethod.MethodName}）"),
                            selectedMethodIndex);

                        // 倒序压栈，保证子任务按声明顺序被处理
                        if (selectedMethod.SubTasks != null)
                        {
                            for (int i = selectedMethod.SubTasks.Count - 1; i >= 0; i--)
                            {
                                HTNTask subTask = selectedMethod.SubTasks[i];

                                if (subTask != null)
                                {
                                    tasksToProcess.Add(subTask);
                                }
                            }
                        }

                        // 重试槽用完即清，后续同名复合任务的其他出现从头分解
                        if (compound == retryTarget)
                        {
                            retryTarget = null;
                            retryMethodIndex = 0;
                        }

                        continue;
                    }


                    // 没有可用方法：回溯
                    AddLog(
                        log,
                        HTNPlanLogEntry.Kind.CompoundFailed,
                        compound,
                        $"{compound.name} 找不到可用的实现方法" +
                        (mtrLimit != null && mtrLimit.ContainsKey(compound)
                            ? "（受当前计划 MTR 优先级约束）"
                            : ""),
                        -1);

                    if (!RestoreToLastDecomposition(
                            history,
                            tasksToProcess,
                            finalPlan,
                            mtr,
                            workingState,
                            ref retryTarget,
                            ref retryMethodIndex))
                    {
                        plan.FailReason =
                            $"复合任务 {compound.name} 无法分解，且没有可回溯的分解记录";

                        return plan;
                    }

                    continue;
                }


                // ---- 基元任务：校验条件 → 加入计划 → 施加效果 ----
                if (task is HTNPrimitiveTask primitive)
                {
                    if (primitive.AreConditionsSatisfied(workingState))
                    {
                        finalPlan.Add(primitive);

                        // 假定任务一定成功：普通 + 期望效果都施加到工作状态，
                        // 之后的分解决策都基于“未来已经发生”的状态进行
                        primitive.ApplyEffects(
                            workingState,
                            true);

                        AddLog(
                            log,
                            HTNPlanLogEntry.Kind.PrimitiveAccepted,
                            primitive,
                            $"基元任务 {primitive.name} 加入计划（操作：{primitive.OperatorId}）",
                            -1);

                        continue;
                    }


                    // 条件不满足：回溯
                    AddLog(
                        log,
                        HTNPlanLogEntry.Kind.PrimitiveRejected,
                        primitive,
                        $"基元任务 {primitive.name} 条件不满足（{primitive.DescribeConditions()}）",
                        -1);

                    if (!RestoreToLastDecomposition(
                            history,
                            tasksToProcess,
                            finalPlan,
                            mtr,
                            workingState,
                            ref retryTarget,
                            ref retryMethodIndex))
                    {
                        plan.FailReason =
                            $"基元任务 {primitive.name} 条件不满足，且没有可回溯的分解记录";

                        return plan;
                    }

                    continue;
                }
            }


            // ---- 完成 ----
            plan.Success = true;

            AddLog(
                log,
                HTNPlanLogEntry.Kind.Decompose,
                null,
                $"规划完成：{plan.Describe()}",
                -1);

            return plan;
        }


        // =========================================================
        // 回溯
        // =========================================================

        /// <summary>
        /// 还原到上一次分解（第 12.4 节 RestoreToLastDecomposedTask）：
        /// 从分解历史栈弹出最近的快照，恢复待处理栈 / 计划 / MTR，
        /// 把该复合任务重新压回栈并从下一个方法继续尝试。
        /// </summary>
        /// <returns>历史栈为空（无法回溯）时返回 false。</returns>
        private bool RestoreToLastDecomposition(
            Stack<DecompositionHistory> history,
            List<HTNTask> tasksToProcess,
            List<HTNPrimitiveTask> finalPlan,
            List<HTNMtrEntry> mtr,
            HTNWorldState workingState,
            ref HTNCompoundTask retryTarget,
            ref int retryMethodIndex)
        {
            if (history.Count == 0)
            {
                return false;
            }

            DecompositionHistory entry = history.Pop();

            // 恢复快照
            tasksToProcess.Clear();
            tasksToProcess.AddRange(entry.TasksSnapshot);

            if (finalPlan.Count > entry.PlanCount)
            {
                finalPlan.RemoveRange(
                    entry.PlanCount,
                    finalPlan.Count - entry.PlanCount);
            }

            if (mtr.Count > entry.MtrCount)
            {
                mtr.RemoveRange(
                    entry.MtrCount,
                    mtr.Count - entry.MtrCount);
            }

            // 工作世界状态一并还原到该分解发生时
            workingState.RestoreFrom(entry.WorkingStateSnapshot);

            // 把复合任务重新压回，稍后从被拒方法的下一个方法继续尝试
            tasksToProcess.Add(entry.CompoundTask);

            retryTarget = entry.CompoundTask;
            retryMethodIndex = entry.TriedMethodIndex + 1;

            return true;
        }


        /// <summary>
        /// 把当前运行中计划的 MTR 转成约束表：复合任务 → 允许的最大方法索引。
        /// </summary>
        private static Dictionary<HTNCompoundTask, int> BuildMtrLimit(
            HTNPlan lastPlan)
        {
            if (lastPlan == null ||
                lastPlan.Mtr.Count == 0)
            {
                return null;
            }

            Dictionary<HTNCompoundTask, int> limit =
                new Dictionary<HTNCompoundTask, int>();

            foreach (HTNMtrEntry entry in lastPlan.Mtr)
            {
                if (entry.CompoundTask == null)
                {
                    continue;
                }

                if (limit.TryGetValue(entry.CompoundTask, out int current))
                {
                    if (entry.MethodIndex < current)
                    {
                        limit[entry.CompoundTask] = entry.MethodIndex;
                    }
                }
                else
                {
                    limit[entry.CompoundTask] = entry.MethodIndex;
                }
            }

            return limit;
        }


        // =========================================================
        // 日志
        // =========================================================

        private static void AddLog(
            List<HTNPlanLogEntry> log,
            HTNPlanLogEntry.Kind kind,
            HTNTask task,
            string message,
            int methodIndex)
        {
            log?.Add(new HTNPlanLogEntry
            {
                EntryKind = kind,
                Task = task,
                Message = message,
                MethodIndex = methodIndex,
            });
        }
    }
}
