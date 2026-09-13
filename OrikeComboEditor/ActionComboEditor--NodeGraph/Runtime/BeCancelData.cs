using System;
using UnityEngine;

namespace Orike.ActionGraph
{
    /// <summary>
    /// BeCancel 数据（被取消方）。
    ///
    /// 表示当前 Action 在某个时间窗口内允许被指定 Tag 的动作取消。
    /// 例如攻击动作 0.3s ~ 0.6s 允许被闪避取消。
    /// </summary>
    [Serializable]
    public class BeCancelData
    {
        /// <summary>
        /// 被取消标签，与取消方 CancelData.Tag 配对。
        /// </summary>
        public string Tag;

        /// <summary>
        /// 窗口开始时间（秒）。
        /// </summary>
        public float StartTime;

        /// <summary>
        /// 窗口结束时间（秒）。
        /// </summary>
        public float EndTime =
            1f;


        public BeCancelData()
        {
        }


        public BeCancelData(
            string tag,
            float startTime = 0f,
            float endTime = 1f)
        {
            Tag =
                tag;

            StartTime =
                startTime;

            EndTime =
                endTime;
        }


        /// <summary>
        /// 指定动作播放时间是否处于取消窗口内。
        /// </summary>
        public bool ContainsTime(
            float actionTime)
        {
            return
                actionTime >= StartTime &&
                actionTime <= EndTime;
        }
    }
}
