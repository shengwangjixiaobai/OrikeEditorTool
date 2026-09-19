using System.IO;
using UnityEditor;
using UnityEngine;

namespace Orike.HTN.Editor
{
    /// <summary>
    /// HTNDomain / HTNTask 子资源的创建、删除、引用维护。
    /// 所有结构修改都注册 Undo 并标记资产脏。
    /// </summary>
    public static class HTNDomainAssetOps
    {
        /// <summary>
        /// 创建新的 HTN 定义域资产（弹出保存面板），
        /// 并自动创建一个根复合任务。
        /// </summary>
        public static HTNDomain CreateDomainAsset()
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
                    "新建 HTN 定义域",
                    "HTNDomain",
                    "asset",
                    "选择 HTN 定义域保存位置",
                    selectedPath);

            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            HTNDomain domain =
                ScriptableObject.CreateInstance<HTNDomain>();

            AssetDatabase.CreateAsset(
                domain,
                path);

            // 预置根复合任务，保证域可以直接开始配置
            HTNCompoundTask root =
                CreateCompoundTask(domain, "Root");

            if (root != null)
            {
                domain.RootTask =
                    root;
            }

            EditorUtility.SetDirty(domain);

            AssetDatabase.SaveAssets();

            Selection.activeObject =
                domain;

            EditorGUIUtility.PingObject(domain);

            return domain;
        }


        /// <summary>
        /// 在域内创建一个复合任务子资源。
        /// </summary>
        public static HTNCompoundTask CreateCompoundTask(
            HTNDomain domain,
            string taskName = null)
        {
            HTNCompoundTask task =
                ScriptableObject.CreateInstance<HTNCompoundTask>();

            RegisterNewTask(
                domain,
                task,
                taskName,
                "新复合任务",
                "Add HTN Compound Task");

            return task;
        }


        /// <summary>
        /// 在域内创建一个基元任务子资源。
        /// </summary>
        public static HTNPrimitiveTask CreatePrimitiveTask(
            HTNDomain domain,
            string taskName = null)
        {
            HTNPrimitiveTask task =
                ScriptableObject.CreateInstance<HTNPrimitiveTask>();

            RegisterNewTask(
                domain,
                task,
                taskName,
                "新基元任务",
                "Add HTN Primitive Task");

            return task;
        }


        private static void RegisterNewTask(
            HTNDomain domain,
            HTNTask task,
            string taskName,
            string fallbackName,
            string undoName)
        {
            Undo.RegisterCreatedObjectUndo(
                task,
                undoName);

            task.name =
                string.IsNullOrEmpty(taskName)
                    ? UniqueTaskName(domain, fallbackName)
                    : taskName;

            AssetDatabase.AddObjectToAsset(
                task,
                domain);

            Undo.RecordObject(
                domain,
                "Edit HTN Domain");

            domain.Tasks.Add(task);

            EditorUtility.SetDirty(domain);
        }


        /// <summary>
        /// 删除任务子资源，并清理所有实现方法里对它的引用。
        /// </summary>
        public static void DeleteTask(
            HTNDomain domain,
            HTNTask task)
        {
            if (domain == null ||
                task == null)
            {
                return;
            }

            Undo.IncrementCurrentGroup();

            Undo.RecordObject(
                domain,
                "Edit HTN Domain");

            // 清理域内所有方法对它的引用
            foreach (HTNTask other in domain.Tasks)
            {
                if (other is HTNCompoundTask compound)
                {
                    foreach (HTNMethod method in compound.Methods)
                    {
                        method?.SubTasks.Remove(task);
                    }
                }
            }

            // 根任务被删时回退到第一个复合任务
            if (domain.RootTask == task)
            {
                domain.RootTask =
                    domain.Tasks.Find(
                        t => t != null &&
                             t is HTNCompoundTask &&
                             t != task) as HTNCompoundTask;
            }

            domain.Tasks.Remove(task);

            AssetDatabase.RemoveObjectFromAsset(task);

            Undo.DestroyObjectImmediate(task);

            EditorUtility.SetDirty(domain);

            AssetDatabase.SaveAssets();
        }


        /// <summary>
        /// 设置根任务。
        /// </summary>
        public static void SetRootTask(
            HTNDomain domain,
            HTNTask task)
        {
            if (domain == null ||
                !(task is HTNCompoundTask))
            {
                return;
            }

            Undo.RecordObject(
                domain,
                "Edit HTN Domain");

            domain.RootTask = task;

            EditorUtility.SetDirty(domain);
        }


        /// <summary>
        /// 重命名任务（会改变子资源名，编辑器各处按名字显示）。
        /// </summary>
        public static void RenameTask(
            HTNTask task,
            string newName)
        {
            if (task == null ||
                string.IsNullOrEmpty(newName) ||
                task.name == newName)
            {
                return;
            }

            Undo.RecordObject(
                task,
                "Rename HTN Task");

            task.name = newName;

            EditorUtility.SetDirty(task);
        }


        /// <summary>生成不重名的任务默认名。</summary>
        public static string UniqueTaskName(
            HTNDomain domain,
            string baseName)
        {
            int index = 1;

            while (true)
            {
                string candidate =
                    $"{baseName}_{index}";

                bool exists =
                    domain.Tasks.Exists(
                        t => t != null && t.name == candidate);

                if (!exists)
                {
                    return candidate;
                }

                index++;
            }
        }
    }
}
