using System;
using System.Collections.Generic;
using UnityEngine;

namespace Orike.ActionGraph
{
    /// <summary>
    /// Action Graph 数据资产。
    ///
    /// 保存：
    ///   - Action 列表
    ///   - Transition 列表（节点之间的 Edge）
    /// </summary>
    [CreateAssetMenu(
        fileName = "ActionGraphData",
        menuName = "Orike/Action Graph Data")]
    public class ActionGraphData : ScriptableObject
    {
        /// <summary>
        /// 图中全部 Action（每个 Action 是该资产的子资源）。
        /// </summary>
        public List<Action> Actions =
            new List<Action>();

        /// <summary>
        /// 图中全部 Transition（Edge）。
        /// </summary>
        public List<TransitionData> Transitions =
            new List<TransitionData>();


        // =========================================================
        // 查询
        // =========================================================

        public Action GetAction(
            string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            foreach (Action action in Actions)
            {
                if (action != null &&
                    action.Id == id)
                {
                    return action;
                }
            }

            return null;
        }


        /// <summary>
        /// 获取 From -> To 的 Tag 取消 Transition；不存在返回 null。
        /// 自动转移连线（Auto == true）不在此列。
        /// </summary>
        public TransitionData GetTransition(
            Action from,
            Action to)
        {
            if (from == null ||
                to == null)
            {
                return null;
            }

            foreach (TransitionData transition in Transitions)
            {
                if (transition != null &&
                    !transition.Auto &&
                    transition.From == from &&
                    transition.To == to)
                {
                    return transition;
                }
            }

            return null;
        }


        /// <summary>
        /// 获取 From 动作的“结束自动转移”Transition；不存在返回 null。
        /// 每个非 Loop 动作至多有一条。
        /// </summary>
        public TransitionData GetAutoTransition(
            Action from)
        {
            if (from == null)
            {
                return null;
            }

            foreach (TransitionData transition in Transitions)
            {
                if (transition != null &&
                    transition.Auto &&
                    transition.From == from)
                {
                    return transition;
                }
            }

            return null;
        }


        /// <summary>
        /// 是否存在 From -> To 的连线。
        /// </summary>
        public bool HasTransition(
            Action from,
            Action to)
        {
            return
                GetTransition(from, to) != null;
        }


        /// <summary>
        /// 是否存在任意一条指向 to 的连线。
        /// </summary>
        public bool HasIncomingTransition(
            Action to)
        {
            if (to == null)
            {
                return false;
            }

            foreach (TransitionData transition in Transitions)
            {
                if (transition != null &&
                    transition.To == to)
                {
                    return true;
                }
            }

            return false;
        }


        /// <summary>
        /// 判断 to 是否可以取消 from：
        /// Tag 配对成功即成立（窗口由运行时根据播放时间判断）。
        /// </summary>
        public bool CanCancel(
            Action from,
            Action to)
        {
            if (from == null ||
                to == null ||
                from == to)
            {
                return false;
            }

            foreach (BeCancelData beCancel in from.BeCancels)
            {
                if (beCancel == null ||
                    string.IsNullOrEmpty(beCancel.Tag))
                {
                    continue;
                }

                if (to.HasCancelTag(beCancel.Tag))
                {
                    return true;
                }
            }

            return false;
        }


        // =========================================================
        // 结构维护（编辑器连线时调用）
        // =========================================================

        /// <summary>
        /// 创建一条 From -> To 的 Transition（Edge）。
        ///
        /// 不再自动生成 Cancel / BeCancel，改为校验：
        ///   From 的 BeCancels 与 To 的 Cancels 必须存在相同 Tag，
        ///   才允许创建连线。Tag 由用户在 Inspector 中手动配置。
        ///
        /// 已存在相同连线时直接返回原 Transition。
        /// 没有匹配 Tag 时返回 null。
        /// </summary>
        public TransitionData Connect(
            Action from,
            Action to)
        {
            if (from == null ||
                to == null ||
                from == to)
            {
                return null;
            }

            TransitionData existing =
                GetTransition(from, to);

            if (existing != null)
            {
                return existing;
            }


            // 校验：必须存在匹配的 Tag
            if (FindMatchingTag(from, to) == null)
            {
                return null;
            }


            TransitionData transition =
                new TransitionData(from, to);

            Transitions.Add(transition);

            return transition;
        }


        /// <summary>
        /// 创建一条 From 自然播放结束后自动切换到 To 的 Transition。
        ///
        /// 与 Tag 取消连线不同：
        ///   - 不校验 Cancel / BeCancel Tag；
        ///   - 每个 From 至多一条，已存在时返回 null；
        ///   - From 为 Loop 动作时数据仍允许存在
        ///     （运行时不生效，编辑器中隐藏端口与连线）。
        /// </summary>
        public TransitionData ConnectAuto(
            Action from,
            Action to)
        {
            if (from == null ||
                to == null ||
                from == to)
            {
                return null;
            }

            if (GetAutoTransition(from) != null)
            {
                return null;
            }

            TransitionData transition =
                new TransitionData(from, to)
                {
                    Auto =
                        true,
                };

            Transitions.Add(transition);

            return transition;
        }


        /// <summary>
        /// 查找 From.BeCancels 与 To.Cancels 之间第一个匹配的 Tag。
        /// 不存在匹配时返回 null。
        /// </summary>
        public string FindMatchingTag(
            Action from,
            Action to)
        {
            if (from == null ||
                to == null ||
                from.BeCancels == null ||
                to.Cancels == null)
            {
                return null;
            }

            foreach (BeCancelData beCancel in from.BeCancels)
            {
                if (beCancel == null ||
                    string.IsNullOrEmpty(beCancel.Tag))
                {
                    continue;
                }

                if (to.HasCancelTag(beCancel.Tag))
                {
                    return beCancel.Tag;
                }
            }

            return null;
        }


        /// <summary>
        /// 移除与指定 Action 相关、且已无匹配 Tag 的孤立 Transition。
        /// 通常在删除 Cancel / BeCancel 后调用，避免数据残留。
        /// </summary>
        public void RemoveOrphanedTransitions(
            Action action)
        {
            if (action == null ||
                Transitions == null)
            {
                return;
            }

            for (int i = Transitions.Count - 1;
                 i >= 0;
                 i--)
            {
                TransitionData transition =
                    Transitions[i];

                if (transition == null)
                {
                    Transitions.RemoveAt(i);

                    continue;
                }

                if (transition.From == action ||
                    transition.To == action)
                {
                    // 自动转移连线不依赖 Tag 配对，不能因 Tag 失配被清理
                    if (transition.Auto)
                    {
                        continue;
                    }

                    if (FindMatchingTag(
                            transition.From,
                            transition.To) ==
                        null)
                    {
                        Transitions.RemoveAt(i);
                    }
                }
            }
        }


        /// <summary>
        /// 删除指定的一条 Transition（按引用精确删除）。
        /// 同一对 Action 之间可能同时存在 Tag 取消连线与自动转移连线，
        /// 不能误删另一条。
        /// </summary>
        public bool Disconnect(
            TransitionData transition)
        {
            if (transition == null)
            {
                return false;
            }

            bool removed =
                false;

            for (int i = Transitions.Count - 1;
                 i >= 0;
                 i--)
            {
                if (Transitions[i] == transition)
                {
                    Transitions.RemoveAt(i);

                    removed =
                        true;
                }
            }

            return removed;
        }


        /// <summary>
        /// 删除 From -> To 的 Transition。
        /// Cancel / BeCancel 由用户手动管理，这里不再自动清理。
        /// </summary>
        public bool Disconnect(
            Action from,
            Action to)
        {
            if (from == null ||
                to == null)
            {
                return false;
            }

            bool removed =
                false;

            for (int i = Transitions.Count - 1;
                 i >= 0;
                 i--)
            {
                TransitionData transition =
                    Transitions[i];

                if (transition != null &&
                    transition.From == from &&
                    transition.To == to)
                {
                    Transitions.RemoveAt(i);

                    removed =
                        true;
                }
            }

            return removed;
        }


        /// <summary>
        /// 删除 Action 以及与它相关的全部 Transition。
        /// </summary>
        public void RemoveAction(
            Action action)
        {
            if (action == null)
            {
                return;
            }

            // 先断开全部相关连线（清理 Cancel / BeCancel）
            for (int i = Transitions.Count - 1;
                 i >= 0;
                 i--)
            {
                TransitionData transition =
                    Transitions[i];

                if (transition == null)
                {
                    Transitions.RemoveAt(i);

                    continue;
                }

                if (transition.From == action ||
                    transition.To == action)
                {
                    Disconnect(transition);
                }
            }

            Actions.Remove(action);
        }


        /// <summary>
        /// 清理空引用并保证 Id 唯一。
        /// </summary>
        public void RefreshData()
        {
            if (Actions == null)
            {
                Actions =
                    new List<Action>();
            }

            if (Transitions == null)
            {
                Transitions =
                    new List<TransitionData>();
            }

            Actions.RemoveAll(
                action => action == null);

            Transitions.RemoveAll(
                transition =>
                    transition == null ||
                    transition.From == null ||
                    transition.To == null);
        }
    }
}
