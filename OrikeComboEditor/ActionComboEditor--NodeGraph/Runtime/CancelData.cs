using System;

namespace Orike.ActionGraph
{
    /// <summary>
    /// Cancel 数据（取消方）。
    ///
    /// 表示当前 Action 拥有“主动取消其他动作”的能力。
    /// 匹配规则：
    ///   ActionB.BeCancel.Tags 中包含 ActionA.Cancel.Tag
    /// 成立时，ActionA 可以取消 ActionB。
    /// </summary>
    [Serializable]
    public class CancelData
    {
        /// <summary>
        /// 取消标签，与被取消方 BeCancelData.Tags 中的任意一项配对。
        /// 连线时默认使用取消方（To 节点）的 Action.Id。
        /// </summary>
        public string Tag;


        public CancelData()
        {
        }


        public CancelData(
            string tag)
        {
            Tag =
                tag;
        }
    }
}
