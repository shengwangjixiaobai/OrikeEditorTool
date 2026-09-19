using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Orike.ActionGraph
{
    /// <summary>
    /// BeCancel 数据（被取消方）。
    ///
    /// 表示当前 Action 在某个时间窗口内允许被指定 Tag 的动作取消。
    /// 例如攻击动作 0.3s ~ 0.6s 允许被闪避取消。
    ///
    /// 一个窗口可配置多个 Tag，目标动作只需匹配其中任意一个即可取消。
    /// </summary>
    [Serializable]
    public class BeCancelData
    {
        /// <summary>
        /// 被取消标签列表，与取消方 CancelData.Tag 配对。
        /// 目标动作只需拥有其中任意一个 Tag 即视为匹配。
        /// </summary>
        public List<string> Tags =
            new List<string>();

        /// <summary>
        /// 窗口开始时间（归一化，0~1，相对动作总时长）。
        /// </summary>
        [Range(0f, 1f)]
        public float StartTime;

        /// <summary>
        /// 窗口结束时间（归一化，0~1，相对动作总时长）。
        /// </summary>
        [Range(0f, 1f)]
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
            Tags =
                new List<string>
                {
                    tag,
                };

            StartTime =
                startTime;

            EndTime =
                endTime;
        }


        /// <summary>
        /// 是否包含指定 Tag。
        /// </summary>
        public bool HasTag(
            string tag)
        {
            if (string.IsNullOrEmpty(tag) ||
                Tags == null)
            {
                return false;
            }

            foreach (string t in Tags)
            {
                if (t == tag)
                {
                    return true;
                }
            }

            return false;
        }


        /// <summary>
        /// 是否有任意有效 Tag。
        /// </summary>
        public bool HasAnyTag()
        {
            if (Tags == null)
            {
                return false;
            }

            foreach (string t in Tags)
            {
                if (!string.IsNullOrEmpty(t))
                {
                    return true;
                }
            }

            return false;
        }


        /// <summary>
        /// 所有 Tag 的显示文本（逗号分隔）。
        /// </summary>
        public string TagsString
        {
            get
            {
                return Tags != null && Tags.Count > 0
                    ? string.Join(", ", Tags)
                    : "";
            }
        }


        /// <summary>
        /// 从逗号分隔的字符串解析 Tag 列表。
        /// </summary>
        public void SetTagsFromString(
            string text)
        {
            Tags =
                text
                    .Split(',')
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();

            if (Tags.Count == 0)
            {
                Tags =
                    new List<string>
                    {
                        "",
                    };
            }
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
