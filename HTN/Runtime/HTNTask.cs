using UnityEngine;

namespace Orike.HTN
{
    /// <summary>
    /// HTN 任务基类。
    ///
    /// 两种具体类型：
    ///   - HTNCompoundTask 复合任务：高层抽象任务的容器，由若干实现方法组成，
    ///     计划执行阶段不会运行任何复合任务代码；
    ///   - HTNPrimitiveTask 基元任务：计划的最小执行单位。
    ///
    /// 每个任务都是所属 HTNDomain 的子资源（Sub-Asset），
    /// EditorPosition 只保存编辑器画布布局，与运行逻辑无关。
    /// </summary>
    public abstract class HTNTask : ScriptableObject
    {
        /// <summary>任务说明（编辑器显示）。</summary>
        [TextArea]
        public string Description;

        /// <summary>编辑器画布中的节点位置（仅编辑器布局用）。</summary>
        [SerializeField]
        private Vector2 editorPosition;

        /// <summary>编辑器画布中的节点位置。</summary>
        public Vector2 EditorPosition
        {
            get => editorPosition;
            set => editorPosition = value;
        }


        /// <summary>是否为复合任务。</summary>
        public bool IsCompound => this is HTNCompoundTask;


        /// <summary>任务类型中文短名（节点标题栏 / 日志显示）。</summary>
        public string TypeName =>
            IsCompound ? "复合" : "基元";
    }
}
