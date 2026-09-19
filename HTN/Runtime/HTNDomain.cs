using System;
using System.Collections.Generic;
using UnityEngine;

namespace Orike.HTN
{
    /// <summary>
    /// HTN 定义域：描述一整套任务层级，代表解决某类问题的所有方式。
    ///
    /// 保存：
    ///   - 世界状态属性定义（属性名 / 默认值）
    ///   - 全部任务（复合 + 基元，均为本资产的子资源）
    ///   - 根复合任务：表示“要解决的问题”，规划从它开始分解
    /// </summary>
    [CreateAssetMenu(
        fileName = "HTNDomain",
        menuName = "Orike/HTN/HTN 定义域")]
    public class HTNDomain : ScriptableObject
    {
        /// <summary>世界状态属性定义（下标即运行期世界状态数组的索引）。</summary>
        public List<HTNWorldStateProperty> WorldStateProperties =
            new List<HTNWorldStateProperty>();

        /// <summary>域内全部任务（每个任务都是本资产的子资源）。</summary>
        public List<HTNTask> Tasks =
            new List<HTNTask>();

        /// <summary>根任务：规划器从这个复合任务开始分解。</summary>
        public HTNTask RootTask;


        // =========================================================
        // 属性名 → 下标缓存
        // =========================================================

        [NonSerialized]
        private Dictionary<string, int> _propertyIndexMap;


        /// <summary>
        /// 取属性下标；属性不存在返回 -1。
        /// </summary>
        public int PropertyIndexOf(string propertyName)
        {
            EnsurePropertyIndexMap();

            if (propertyName != null &&
                _propertyIndexMap.TryGetValue(propertyName, out int index))
            {
                return index;
            }

            return -1;
        }


        private void EnsurePropertyIndexMap()
        {
            if (_propertyIndexMap != null &&
                _propertyIndexMap.Count == WorldStateProperties.Count)
            {
                return;
            }

            _propertyIndexMap =
                new Dictionary<string, int>(
                    WorldStateProperties.Count);

            for (int i = 0; i < WorldStateProperties.Count; i++)
            {
                HTNWorldStateProperty property = WorldStateProperties[i];

                if (property == null ||
                    string.IsNullOrEmpty(property.Name))
                {
                    continue;
                }

                _propertyIndexMap[property.Name] = i;
            }
        }


        /// <summary>属性数量。</summary>
        public int PropertyCount =>
            WorldStateProperties.Count;


        // =========================================================
        // 世界状态
        // =========================================================

        /// <summary>
        /// 按属性默认值创建一份世界状态。
        /// </summary>
        public HTNWorldState CreateWorldState()
        {
            EnsurePropertyIndexMap();

            int[] values = new int[WorldStateProperties.Count];

            for (int i = 0; i < WorldStateProperties.Count; i++)
            {
                HTNWorldStateProperty property = WorldStateProperties[i];

                values[i] = property != null
                    ? property.DefaultValue
                    : 0;
            }

            return new HTNWorldState(
                values,
                _propertyIndexMap);
        }


        // =========================================================
        // 任务查询
        // =========================================================

        /// <summary>
        /// 按名字查找任务；找不到返回 null。
        /// </summary>
        public HTNTask GetTask(string taskName)
        {
            return Tasks.Find(
                task => task != null &&
                        task.name == taskName);
        }


        /// <summary>
        /// 引用完整性检查：清理所有实现方法里已丢失（null）的子任务引用。
        /// 在资源删除 / 重命名错乱后由编辑器调用。
        /// </summary>
        public void RemoveNullReferences()
        {
            foreach (HTNTask task in Tasks)
            {
                if (task is HTNCompoundTask compound)
                {
                    foreach (HTNMethod method in compound.Methods)
                    {
                        method?.SubTasks.RemoveAll(
                            subTask => subTask == null);
                    }
                }
            }

            if (RootTask == null &&
                Tasks.Count > 0)
            {
                RootTask =
                    Tasks.Find(task => task is HTNCompoundTask);
            }
        }
    }
}
