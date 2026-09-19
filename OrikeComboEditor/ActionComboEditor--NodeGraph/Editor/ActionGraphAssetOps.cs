using System.IO;
using UnityEditor;
using UnityEngine;

namespace Orike.ActionGraph
{
    /// <summary>
    /// ActionGraphData / Action 子资源的创建、删除、保存操作。
    /// 所有结构修改都注册 Undo。
    /// </summary>
    public static class ActionGraphAssetOps
    {
        /// <summary>
        /// 创建新的 ActionGraphData 资产（弹出保存面板）。
        /// </summary>
        public static ActionGraphData CreateGraphAsset()
        {
            string selectedPath =
                AssetDatabase.GetAssetPath(
                    Selection.activeObject);

            if (string.IsNullOrEmpty(selectedPath))
            {
                selectedPath =
                    "Assets";
            }
            else
            {
                selectedPath =
                    Directory.Exists(selectedPath)
                        ? selectedPath
                        : Path.GetDirectoryName(selectedPath);
            }

            string path =
                EditorUtility.SaveFilePanelInProject(
                    "新建 Action Graph",
                    "NewActionGraph",
                    "asset",
                    "选择 Action Graph 保存位置",
                    selectedPath);

            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            ActionGraphData graph =
                ScriptableObject.CreateInstance<ActionGraphData>();

            AssetDatabase.CreateAsset(
                graph,
                path);

            AssetDatabase.SaveAssets();

            Selection.activeObject =
                graph;

            EditorGUIUtility.PingObject(
                graph);

            return graph;
        }


        /// <summary>
        /// 在 graph 内创建一个 Action 子资源。
        /// </summary>
        public static Action CreateAction(
            ActionGraphData graph,
            Vector2 nodePosition)
        {
            if (graph == null)
            {
                return null;
            }

            Action action =
                ScriptableObject.CreateInstance<Action>();

            action.Id =
                UniqueId(
                    graph,
                    "Action");

            action.name =
                action.Id;

            action.NodePosition =
                nodePosition;

            RegisterNewAction(
                graph,
                action,
                "Add Action");

            return action;
        }


        /// <summary>
        /// 从序列化 JSON 复制出一个 Action 子资源（复制粘贴用）。
        /// 保留原 Action 的字段（ActionData、Cancel/BeCancel、KeyCommands 等），
        /// 生成新的唯一 Id 并放在指定位置。
        /// </summary>
        public static Action PasteAction(
            ActionGraphData graph,
            string json,
            Vector2 nodePosition)
        {
            if (graph == null ||
                string.IsNullOrEmpty(json))
            {
                return null;
            }

            Action action =
                ScriptableObject.CreateInstance<Action>();

            EditorJsonUtility.FromJsonOverwrite(
                json,
                action);

            action.Id =
                UniqueId(
                    graph,
                    string.IsNullOrEmpty(action.Id)
                        ? "Action"
                        : action.Id);

            action.name =
                action.Id;

            action.NodePosition =
                nodePosition;

            RegisterNewAction(
                graph,
                action,
                "Paste Action");

            return action;
        }


        /// <summary>
        /// 将新建的 Action 注册为 graph 的子资源，并登记 Undo / 保存。
        /// </summary>
        private static void RegisterNewAction(
            ActionGraphData graph,
            Action action,
            string undoName)
        {
            AssetDatabase.AddObjectToAsset(
                action,
                graph);

            Undo.RegisterCreatedObjectUndo(
                action,
                undoName);

            Undo.RecordObject(
                graph,
                undoName);

            graph.Actions.Add(
                action);

            EditorUtility.SetDirty(
                action);

            EditorUtility.SetDirty(
                graph);

            AssetDatabase.SaveAssetIfDirty(
                graph);
        }


        /// <summary>
        /// 删除 Action 子资源及其全部连线。
        /// </summary>
        public static void DeleteAction(
            ActionGraphData graph,
            Action action)
        {
            if (graph == null ||
                action == null)
            {
                return;
            }

            Undo.RecordObject(
                graph,
                "Delete Action");

            graph.RemoveAction(
                action);

            Undo.DestroyObjectImmediate(
                action);

            EditorUtility.SetDirty(
                graph);

            AssetDatabase.SaveAssetIfDirty(
                graph);
        }


        /// <summary>
        /// 注册一次连线修改的 Undo（graph 与两个 Action 都要记录）。
        /// </summary>
        public static void RecordGraphStructureUndo(
            ActionGraphData graph,
            Action from,
            Action to,
            string name)
        {
            if (graph != null)
            {
                Undo.RecordObject(
                    graph,
                    name);
            }

            if (from != null)
            {
                Undo.RecordObject(
                    from,
                    name);
            }

            if (to != null)
            {
                Undo.RecordObject(
                    to,
                    name);
            }
        }


        /// <summary>
        /// 标记连线相关对象为 Dirty 并保存。
        /// </summary>
        public static void SetGraphAndActionsDirty(
            ActionGraphData graph,
            Action from,
            Action to)
        {
            if (graph != null)
            {
                EditorUtility.SetDirty(
                    graph);
            }

            if (from != null)
            {
                EditorUtility.SetDirty(
                    from);
            }

            if (to != null)
            {
                EditorUtility.SetDirty(
                    to);
            }

            if (graph != null)
            {
                AssetDatabase.SaveAssetIfDirty(
                    graph);
            }
        }


        /// <summary>
        /// 生成 graph 内唯一的 Action Id。
        /// </summary>
        public static string UniqueId(
            ActionGraphData graph,
            string baseId)
        {
            if (string.IsNullOrEmpty(baseId))
            {
                baseId =
                    "Action";
            }

            string candidate =
                baseId;

            int suffix =
                1;

            while (ContainsId(graph, candidate))
            {
                suffix++;

                candidate =
                    $"{baseId}{suffix}";
            }

            return candidate;
        }


        private static bool ContainsId(
            ActionGraphData graph,
            string id)
        {
            if (graph == null)
            {
                return false;
            }

            foreach (Action action in graph.Actions)
            {
                if (action != null &&
                    action.Id == id)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
