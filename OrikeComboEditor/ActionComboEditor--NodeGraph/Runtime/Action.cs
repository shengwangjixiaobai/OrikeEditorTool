using System;
using System.Collections.Generic;
using UnityEngine;

namespace Orike.ActionGraph
{
    /// <summary>
    /// 真正参与游戏逻辑的动作对象。
    ///
    /// Action 负责“什么时候执行这个动作”：
    ///   - 引用 ActionData（执行时播放什么内容）
    ///   - Cancel / BeCancel 关系
    ///   - 输入条件 KeyCommand
    ///   - 基础优先级
    ///
    /// 节点编辑器中的一个 Node 对应一个 Action。
    /// Action 以子资源形式保存在 ActionGraphData 资源内。
    /// </summary>
    [CreateAssetMenu(
        fileName = "Action",
        menuName = "Orike/Action")]
    public class Action : ScriptableObject
    {
        [Tooltip("动作唯一 Id，同时作为自动连线时的 Cancel Tag")]
        public string Id;

        [Tooltip("动作表现资源")]
        public ActionData ActionData;

        [Tooltip("主动取消能力：拥有的取消 Tag")]
        public List<CancelData> Cancels =
            new List<CancelData>();

        [Tooltip("允许被取消：取消 Tag + 有效时间窗口")]
        public List<BeCancelData> BeCancels =
            new List<BeCancelData>();

        [Tooltip("触发该动作的搓招输入条件")]
        public List<KeyCommand> KeyCommands =
            new List<KeyCommand>();

        [Tooltip("基础优先级")]
        public int Priority;

        [Tooltip("播放时是否循环（如 Idle / Run）")]
        public bool Loop;

        // =========================================================
        // 编辑器布局数据
        // =========================================================

        [HideInInspector]
        [SerializeField]
        private Vector2 nodePosition;

        /// <summary>
        /// 节点在 Graph 窗口中的位置。
        /// </summary>
        public Vector2 NodePosition
        {
            get => nodePosition;
            set => nodePosition = value;
        }


        // =========================================================
        // 查询
        // =========================================================

        /// <summary>
        /// 是否拥有指定取消 Tag。
        /// </summary>
        public bool HasCancelTag(
            string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return false;
            }

            foreach (CancelData cancel in Cancels)
            {
                if (cancel != null &&
                    cancel.Tag == tag)
                {
                    return true;
                }
            }

            return false;
        }


        /// <summary>
        /// 查找指定 Tag 的被取消窗口；不存在返回 null。
        /// </summary>
        public BeCancelData FindBeCancel(
            string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return null;
            }

            foreach (BeCancelData beCancel in BeCancels)
            {
                if (beCancel != null &&
                    beCancel.HasTag(tag))
                {
                    return beCancel;
                }
            }

            return null;
        }


        /// <summary>
        /// 当前播放进度（归一化 0~1）是否允许被指定 Tag 取消。
        /// </summary>
        public bool CanBeCanceled(
            string tag,
            float normalizedTime)
        {
            BeCancelData beCancel =
                FindBeCancel(tag);

            return
                beCancel != null &&
                beCancel.ContainsTime(normalizedTime);
        }
    }
}
