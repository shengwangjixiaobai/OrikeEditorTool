using UnityEngine;

namespace Orike.ActionGraph
{
    /// <summary>
    /// ActionData 的只读辅助方法。
    ///
    /// ActionData 属于已有的表现层系统，这里只做查询，不修改其结构。
    /// 时长计算规则与 ActionPlayer 内部规则保持一致：
    /// 所有 Clip 的 EndTime / PointEvent 的 Time 的最大值。
    /// </summary>
    public static class ActionDataUtility
    {
        /// <summary>
        /// 获取动作总时长（秒）。
        /// </summary>
        public static float GetDuration(
            ActionData actionData)
        {
            if (actionData == null ||
                actionData.Tracks == null)
            {
                return 0f;
            }

            float duration =
                0f;

            foreach (TrackData track in actionData.Tracks)
            {
                if (track == null)
                {
                    continue;
                }

                if (track.Clips != null)
                {
                    foreach (BaseClipData clip in track.Clips)
                    {
                        if (clip == null)
                        {
                            continue;
                        }

                        if (clip.EndTime > duration)
                        {
                            duration =
                                clip.EndTime;
                        }
                    }
                }

                if (track.PointEvents != null)
                {
                    foreach (PointEventData pointEvent
                             in track.PointEvents)
                    {
                        if (pointEvent == null)
                        {
                            continue;
                        }

                        if (pointEvent.Time > duration)
                        {
                            duration =
                                pointEvent.Time;
                        }
                    }
                }
            }

            return duration;
        }


        /// <summary>
        /// 获取指定时刻所在的动画 Clip；没有命中时返回第一个动画 Clip。
        /// 与 ActionPlayer 内部取片逻辑一致。
        /// </summary>
        public static AnimationClip GetAnimationClipAt(
            ActionData actionData,
            float time)
        {
            if (actionData == null ||
                actionData.Tracks == null)
            {
                return null;
            }

            AnimationClip fallback =
                null;

            foreach (TrackData track in actionData.Tracks)
            {
                if (track == null ||
                    track.Clips == null)
                {
                    continue;
                }

                foreach (BaseClipData clip in track.Clips)
                {
                    if (!(clip is AnimationClipData animationClip) ||
                        animationClip.Animation == null)
                    {
                        continue;
                    }

                    if (fallback == null)
                    {
                        fallback =
                            animationClip.Animation;
                    }

                    if (animationClip.ContainsTime(time))
                    {
                        return animationClip.Animation;
                    }
                }
            }

            return fallback;
        }


        /// <summary>
        /// 获取动作的第一个动画 Clip（用于预览）。
        /// </summary>
        public static AnimationClip GetFirstAnimationClip(
            ActionData actionData)
        {
            return
                GetAnimationClipAt(
                    actionData,
                    0f);
        }
    }
}
