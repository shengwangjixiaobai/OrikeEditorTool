using System;

namespace Orike.ActionGraph
{
    /// <summary>
    /// 两个 Action 之间的切换规则。
    ///
    /// Transition 属于 Action 之间的连接关系，
    /// 不是 ActionData 的一部分。
    ///
    /// 连线方向（Edge）：
    ///   From（被取消 Action） -> To（执行取消 Action）
    /// </summary>
    [Serializable]
    public class TransitionData
    {
        /// <summary>
        /// 被取消的 Action。
        /// </summary>
        public Action From;

        /// <summary>
        /// 执行取消的 Action。
        /// </summary>
        public Action To;

        /// <summary>
        /// From Action 退出混合时间（秒）。
        /// </summary>
        public float FadeOut =
            0.25f;

        /// <summary>
        /// To Action 进入混合时间（秒）。
        /// </summary>
        public float FadeIn =
            0.25f;

        /// <summary>
        /// 目标动画开始播放位置（0 ~ 1 的百分比）。
        /// </summary>
        [UnityEngine.Range(0f, 1f)]
        public float StartPercent;

        /// <summary>
        /// 该 Transition 的额外切换优先级。
        /// 最终优先级 = Action.Priority + TransitionData.Priority。
        /// </summary>
        public int Priority;


        public TransitionData()
        {
        }


        public TransitionData(
            Action from,
            Action to)
        {
            From =
                from;

            To =
                to;
        }


        /// <summary>
        /// 实际混合使用的时长。
        ///
        /// ActionPlayer 的混合器是同一组权重，
        /// 取 FadeOut / FadeIn 中较长者保证两侧都能完成混合。
        /// 返回值小于等于 0 时由 ActionPlayer 使用默认混合时长。
        /// </summary>
        public float GetBlendDuration()
        {
            float duration =
                UnityEngine.Mathf.Max(
                    FadeOut,
                    FadeIn);

            return duration < 0f
                ? 0f
                : duration;
        }
    }
}
