using System.Text;
using UnityEditor;
using UnityEngine;

namespace Orike.ActionGraph
{
    /// <summary>
    /// 共享的 Action 字段绘制逻辑，供右侧 Inspector 与节点内嵌编辑器复用。
    ///
    /// 负责绘制：
    ///   Id / ActionData / Priority / Loop /
    ///   KeyCommands / Cancels / BeCancels
    ///
    /// 同时处理 Id 唯一性校验与 Id 改名后的自动 Tag 联动。
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

            EditorGUILayout.LabelField(
                "输入条件 KeyCommand",
                EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(
                commandsProperty,
                true);

            if (showCancelLists)
            {
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
                // 节点内嵌编辑器下 KeyCommand 是最后一项，
                // 预留与节点底边的间距，避免贴边。
                EditorGUILayout.Space(8);
            }


            // ---------------------------------------------------------
            // 应用改动
            // ---------------------------------------------------------

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

                if (graph != null)
                {
                    AssetDatabase.SaveAssetIfDirty(
                        graph);
                }
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
                        beCancel.Tag);

                    builder.Append(
                        ',');
                }
            }

            return builder.ToString();
        }
    }
}