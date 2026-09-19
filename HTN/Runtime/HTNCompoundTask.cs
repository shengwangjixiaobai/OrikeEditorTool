using System;
using System.Collections.Generic;
using System.Text;

namespace Orike.HTN
{
    /// <summary>
    /// 复合任务的实现方法。
    ///
    /// 一个实现方法 = 一组预设条件 + 一组子任务。
    /// 条件在当前（工作）世界状态下成立时该方法被选中，
    /// 子任务集（既可以是基元任务也可以是复合任务）表示具体的实现方式。
    /// 方法按列表顺序即优先级顺序参与分解：索引越小优先级越高。
    /// </summary>
    [Serializable]
    public class HTNMethod
    {
        /// <summary>方法名（编辑器显示，如 “近战攻击”）。</summary>
        public string MethodName = "方法";

        /// <summary>预设条件：全部成立时该方法才有机会被选中。</summary>
        public List<HTNCondition> Conditions =
            new List<HTNCondition>();

        /// <summary>子任务序列（顺序即计划中的执行顺序，允许引用自身形成递归）。</summary>
        public List<HTNTask> SubTasks =
            new List<HTNTask>();


        /// <summary>
        /// 判断该方法在给定世界状态下是否可用：
        /// 所有预设条件均成立。
        /// </summary>
        public bool IsSatisfied(HTNWorldState worldState)
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


        /// <summary>条件摘要（节点显示）。</summary>
        public string DescribeConditions()
        {
            if (Conditions == null ||
                Conditions.Count == 0)
            {
                return "无条件";
            }

            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < Conditions.Count; i++)
            {
                if (Conditions[i] == null)
                {
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.Append(" && ");
                }

                sb.Append(Conditions[i].Describe());
            }

            return sb.ToString();
        }
    }


    /// <summary>
    /// 复合任务：体现 HTN “分层” 本质的高层任务容器。
    ///
    /// 拥有多种实现方式（HTNMethod），分解时按方法顺序尝试，
    /// 第一个条件满足的方法被选中，其子任务被压回待处理任务栈继续分解。
    ///
    /// 注意：任务是所属定义域的子资源，由 HTNDomainAssetOps 程序化创建，
    /// 不提供 CreateAssetMenu（避免产生不属于任何域的孤儿任务资产）。
    /// </summary>
    public class HTNCompoundTask : HTNTask
    {
        /// <summary>
        /// 实现方法列表，按优先级排序（索引越小优先级越高）。
        /// </summary>
        public List<HTNMethod> Methods =
            new List<HTNMethod>();
    }
}
