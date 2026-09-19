using System;
using System.Collections.Generic;
using UnityEngine;

namespace Orike.HTN
{
    /// <summary>
    /// 世界状态属性定义（编辑期数据）。
    ///
    /// 《游戏人工智能》第 12 章：世界状态本质是一个属性集合，用枚举作为索引。
    /// 这里用属性名 + 默认值来定义，属性值统一用 int 表示：
    ///   - 布尔属性：0 / 1
    ///   - 枚举属性：枚举的原始值（如 WsEnemyRange 的 0=近 1=中 2=远）
    ///   - 数值属性：生命、弹药、耐久等
    /// 世界状态只需要表达规划器做决策依赖的信息，不必面面俱到。
    /// </summary>
    [Serializable]
    public class HTNWorldStateProperty
    {
        /// <summary>属性名，如 WsCanSeeEnemy。条件 / 效果通过该名字引用属性。</summary>
        public string Name = "WsNewProperty";

        /// <summary>默认值：Agent 创建世界状态时所有属性的初始值。</summary>
        public int DefaultValue;

        /// <summary>备注（编辑器显示，如值域说明 “0=近 1=中 2=远”）。</summary>
        public string Comment;
    }


    /// <summary>
    /// 运行期世界状态：一个按属性索引的 int 数组。
    ///
    /// 规划时会复制一份“工作世界状态”用于模拟任务效果（正向分解的核心），
    /// 因此本类型设计为轻量纯 C# 类，便于快速 Clone。
    /// </summary>
    public class HTNWorldState
    {
        /// <summary>属性值数组，下标即 HTNDomain.WorldStateProperties 中的位置。</summary>
        private int[] _values;

        /// <summary>属性名 → 下标缓存（来自所属 Domain）。</summary>
        private Dictionary<string, int> _indexMap;


        public HTNWorldState(
            int[] values,
            Dictionary<string, int> indexMap)
        {
            _values = values;
            _indexMap = indexMap;
        }


        /// <summary>属性数量。</summary>
        public int PropertyCount => _values.Length;


        /// <summary>按下标读取属性值。</summary>
        public int Get(int index)
        {
            return _values[index];
        }


        /// <summary>按下标写入属性值。</summary>
        public void Set(
            int index,
            int value)
        {
            _values[index] = value;
        }


        /// <summary>按属性名读取；属性不存在返回 false。</summary>
        public bool TryGet(
            string propertyName,
            out int value)
        {
            if (_indexMap != null &&
                _indexMap.TryGetValue(propertyName, out int index))
            {
                value = _values[index];

                return true;
            }

            value = 0;

            return false;
        }


        /// <summary>按属性名写入；属性不存在时返回 false。</summary>
        public bool TrySet(
            string propertyName,
            int value)
        {
            if (_indexMap != null &&
                _indexMap.TryGetValue(propertyName, out int index))
            {
                _values[index] = value;

                return true;
            }

            return false;
        }


        /// <summary>
        /// 复制当前状态。
        /// 规划器用它生成“工作世界状态”来模拟任务执行后的未来。
        /// </summary>
        public HTNWorldState Clone()
        {
            return new HTNWorldState(
                (int[])_values.Clone(),
                _indexMap);
        }


        /// <summary>
        /// 用另一份状态覆盖当前值（用于回溯时还原工作世界状态）。
        /// 两份状态必须来自同一个定义域。
        /// </summary>
        public void RestoreFrom(HTNWorldState source)
        {
            if (source == null ||
                source._values.Length != _values.Length)
            {
                return;
            }

            System.Array.Copy(
                source._values,
                _values,
                _values.Length);
        }
    }
}
