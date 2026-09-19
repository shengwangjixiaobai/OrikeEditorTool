using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Orike.ActionGraph
{
    /// <summary>
    /// 共享的 Action 字段绘制逻辑，供右侧 Inspector 与节点内嵌编辑器复用。
    ///
    /// Inspector（showCancelLists = true）：
    ///   Id / ActionData / Priority / Loop /
    ///   KeyCommands（可编辑） / Cancels / BeCancels
    ///
    /// 节点（showCancelLists = false）：
    ///   Id / ActionData / Priority / Loop 可编辑，
    ///   KeyCommands 仅只读展示按键要求（搓招序列 + 时间窗口），
    ///   配置统一在 Inspector 中进行。
    /// </summary>
    public static class ActionFieldDrawer
    {
        /// <summary>
        /// 一次字段绘制的返回结果。
        /// </summary>
        public readonly struct Result
        {
            /// <summary>本次是否 apply 了任何属性改动。</summary>
            public readonly bool Changed;

            /// <summary>
            /// 是否发生了“影响端口结构”的改动
            /// （Id 改变、Cancels / BeCancels 的 Tag 或条目增减）。
            /// </summary>
            public readonly bool Structural;

            public Result(
                bool changed,
                bool structural)
            {
                Changed =
                    changed;

                Structural =
                    structural;
            }
        }


        public static Result Draw(
            SerializedObject serialized,
            Action action,
            ActionGraphData graph,
            bool showCancelLists = true)
        {
            if (serialized == null ||
                action == null)
            {
                return new Result(
                    false,
                    false);
            }


            // 记录编辑前的端口结构签名，
            // 用于判断是否需要在编辑后整体重建节点端口。
            string beforeSignature =
                BuildPortSignature(action);

            serialized.Update();


            SerializedProperty idProperty =
                serialized.FindProperty("Id");

            SerializedProperty dataProperty =
                serialized.FindProperty("ActionData");

            SerializedProperty priorityProperty =
                serialized.FindProperty("Priority");

            SerializedProperty loopProperty =
                serialized.FindProperty("Loop");

            SerializedProperty commandsProperty =
                serialized.FindProperty("KeyCommands");

            SerializedProperty cancelsProperty =
                serialized.FindProperty("Cancels");

            SerializedProperty beCancelsProperty =
                serialized.FindProperty("BeCancels");


            string previousId =
                action.Id;


            // ---------------------------------------------------------
            // Id（唯一性校验）
            // ---------------------------------------------------------

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(
                idProperty);

            bool idEdited =
                EditorGUI.EndChangeCheck();

            if (idEdited)
            {
                string candidate =
                    idProperty.stringValue;

                Action duplicate =
                    graph != null &&
                    !string.IsNullOrEmpty(candidate)
                        ? graph.GetAction(candidate)
                        : null;

                if (duplicate != null &&
                    duplicate != action)
                {
                    idProperty.stringValue =
                        previousId;

                    EditorGUILayout.HelpBox(
                        $"Id \"{candidate}\" 已存在，已还原。",
                        MessageType.Error);
                }
            }


            // ---------------------------------------------------------
            // 其余字段
            // ---------------------------------------------------------

            EditorGUILayout.PropertyField(
                dataProperty);

            EditorGUILayout.PropertyField(
                priorityProperty);

            EditorGUILayout.PropertyField(
                loopProperty);

            EditorGUILayout.Space(4);

            if (showCancelLists)
            {
                // Inspector：KeyCommands 可编辑
                EditorGUILayout.LabelField(
                    "输入条件 KeyCommand",
                    EditorStyles.boldLabel);

                EditorGUILayout.PropertyField(
                    commandsProperty,
                    true);

                EditorGUILayout.Space(4);

                EditorGUILayout.LabelField(
                    "Cancel（主动取消 Tag）",
                    EditorStyles.boldLabel);

                EditorGUILayout.PropertyField(
                    cancelsProperty,
                    true);

                EditorGUILayout.Space(4);

                EditorGUILayout.LabelField(
                    "BeCancel（被取消窗口）",
                    EditorStyles.boldLabel);

                EditorGUILayout.PropertyField(
                    beCancelsProperty,
                    true);
            }
            else
            {
                // 节点：KeyCommands 只读展示，不提供编辑入口
                DrawKeyCommandsReadOnly(
                    action);

                // 预留与节点底边的间距，避免贴边。
                EditorGUILayout.Space(8);
            }


            // ---------------------------------------------------------
            // 应用改动
            // ---------------------------------------------------------

            // 不在每帧 IMGUI 调用中立即写磁盘，
            // 仅标记 Dirty，避免输入 Id 时每个字符都触发
            // AssetDatabase.SaveAssetIfDirty 导致的 IO 卡顿。
            bool changed =
                serialized.ApplyModifiedProperties();

            if (changed)
            {
                string appliedId =
                    action.Id;

                if (!string.IsNullOrEmpty(appliedId) &&
                    appliedId != previousId)
                {
                    action.name =
                        appliedId;
                }

                EditorUtility.SetDirty(
                    action);
            }

            bool structural =
                changed &&
                BuildPortSignature(action) !=
                beforeSignature;

            return new Result(
                changed,
                structural);
        }


        // =========================================================
        // 节点只读：按键要求
        // =========================================================

        /// <summary>
        /// 在节点内只读展示该动作的按键要求。
        ///
        /// 每条 KeyCommand 是一组“搓招序列”（key 按顺序输入），
        /// 需要在 timeLimit 时间窗口内完成；
        /// 多条 KeyCommand 之间是“或”关系，满足任意一条即可触发。
        /// </summary>
        private static void DrawKeyCommandsReadOnly(
            Action action)
        {
            EditorGUILayout.LabelField(
                "按键要求",
                EditorStyles.boldLabel);

            List<KeyCommand> commands =
                action.KeyCommands;

            if (commands == null ||
                commands.Count == 0)
            {
                EditorGUILayout.LabelField(
                    "无");

                return;
            }

            foreach (KeyCommand command in commands)
            {
                if (command == null ||
                    command.key == null ||
                    command.key.Length == 0)
                {
                    continue;
                }

                // 顺序输入序列用 → 连接，例如：↓ → ↘ → → + A
                string sequence =
                    string.Join(
                        "  →  ",
                        command.key.Select(
                            key => KeyMapGlyph.GetGlyph(
                                key)));

                // 整行绘制（窗口时限附在末尾），
                // 避免窄节点下 LabelField 双列截断序列
                string window =
                    $"  （{command.timeLimit:0.0#}s 内）";

                EditorGUILayout.LabelField(
                    sequence +
                    window,
                    EditorStyles.miniLabel);
            }
        }


        // =========================================================
        // 端口结构签名
        // =========================================================

        private static string BuildPortSignature(
            Action action)
        {
            StringBuilder builder =
                new StringBuilder();

            if (action.Cancels != null)
            {
                foreach (CancelData cancel in action.Cancels)
                {
                    if (cancel == null)
                    {
                        continue;
                    }

                    builder.Append(
                        cancel.Tag);

                    builder.Append(
                        ',');
                }
            }

            builder.Append(
                '|');

            if (action.BeCancels != null)
            {
                foreach (BeCancelData beCancel in action.BeCancels)
                {
                    if (beCancel == null)
                    {
                        continue;
                    }

                    builder.Append(
                        beCancel.TagsString);

                    builder.Append(
                        ',');
                }
            }

            // Loop 决定“结束自动转移”输出端口是否显示，
            // 改变时必须按结构变更整体重建。
            builder.Append(
                '|');

            builder.Append(
                action.Loop
                    ? 'L'
                    : 'N');

            return builder.ToString();
        }
    }
}