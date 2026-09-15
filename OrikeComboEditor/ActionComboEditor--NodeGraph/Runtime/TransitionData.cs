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
    ///
    /// 三个时间参数与 Animator.CrossFade 对齐：
    ///
    ///   TransitionDuration（normalizedTransitionDuration）
    ///     过渡时长。归一化模式下相对于“来源 / 当前动作”的总时长。
    ///
    ///   TimeOffset（normalizedTimeOffset）
    ///     目标动画的起始点。归一化模式下相对于“目标动作”的总时长。
    ///
    ///   TransitionTime（normalizedTransitionTime）
    ///     过渡自身的起始进度（0 ~ 1，始终归一化）。
    ///     例如 0.3：触发过渡时直接以 30% 的混合状态开始，
    ///     跳过前 30% 的渐变；默认 0，从头开始渐变。
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
        /// false（默认）：TransitionDuration / TimeOffset 按归一化配置，
        ///               分别相对来源 / 目标动作总时长；
        /// true：两者按秒配置（对应 CrossFadeInFixedTime）。
        ///
        /// TransitionTime 始终是归一化值，不受该开关影响。
        /// </summary>
        public bool UseFixedTime;

        /// <summary>
        /// 过渡时长。
        /// 归一化模式：相对来源动作总时长（0.25 = 来源时长的 25%）；
        /// 秒模式：单位为秒。
        /// </summary>
        [UnityEngine.Range(0f, 1f)]
        public float TransitionDuration =
            0.25f;

        /// <summary>
        /// 目标动画起始点。
        /// 归一化模式：相对目标动作总时长；
        /// 秒模式：单位为秒。
        /// </summary>
        [UnityEngine.Range(0f, 1f)]
        public float TimeOffset;

        /// <summary>
        /// 过渡自身的起始进度（0 ~ 1，始终归一化）。
        ///
        /// 触发过渡时混合权重直接跳到该进度：
        /// 0（默认）= 从渐变起点开始；1 = 开始即完成（等效瞬切）。
        /// </summary>
        [UnityEngine.Range(0f, 1f)]
        public float TransitionTime;

        /// <summary>
        /// 该 Transition 的额外切换优先级。
        /// 最终优先级 = Action.Priority + TransitionData.Priority。
        /// </summary>
        public int Priority;

        /// <summary>
        /// 是否为“结束自动转移”连线。
        ///
        /// false（默认）：由 Cancel / BeCancel 的 Tag 匹配触发；
        /// true：From 动作播放到末尾前自动开始向 To 过渡，
        ///       不需要 Tag 匹配，也不参与预约 / 优先级竞争。
        ///       From 为 Loop 动作时该连线不生效（端口在编辑器中隐藏）。
        /// </summary>
        public bool Auto;


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
        /// 实际过渡时长（秒）。
        /// 归一化模式 = TransitionDuration × 来源动作总时长；
        /// 秒模式直接取 TransitionDuration。
        /// </summary>
        public float GetFadeDurationSeconds(
            ActionData fromData)
        {
            if (UseFixedTime)
            {
                return TransitionDuration < 0f
                    ? 0f
                    : TransitionDuration;
            }

            return
                UnityEngine.Mathf.Clamp01(
                    TransitionDuration) *
                ActionDataUtility.GetDuration(
                    fromData);
        }


        /// <summary>
        /// 目标动画实际起始时间（秒）。
        /// 归一化模式 = TimeOffset × 目标动作总时长；
        /// 秒模式直接取 TimeOffset。
        /// </summary>
        public float GetStartOffsetSeconds(
            ActionData toData)
        {
            if (UseFixedTime)
            {
                return TimeOffset < 0f
                    ? 0f
                    : TimeOffset;
            }

            return
                UnityEngine.Mathf.Clamp01(
                    TimeOffset) *
                ActionDataUtility.GetDuration(
                    toData);
        }


        /// <summary>
        /// “结束自动转移”实际开始混合的时刻（秒，相对来源动作起点）。
        ///
        /// 取“来源结束时刻 - 剩余混合时长”，
        /// 使过渡的终点恰好落在来源动作播完时：
        ///   TransitionTime 越小（从头渐变），过渡越早开始；
        ///   TransitionTime = 1 时触发点即结尾（等效播完瞬切）。
        /// </summary>
        public float GetAutoTriggerTime(
            ActionData fromData)
        {
            float sourceDuration =
                ActionDataUtility.GetDuration(
                    fromData);

            float fadeDuration =
                GetFadeDurationSeconds(
                    fromData);

            float remainingFade =
                (1f - UnityEngine.Mathf.Clamp01(
                    TransitionTime)) *
                fadeDuration;

            float trigger =
                sourceDuration -
                remainingFade;

            return UnityEngine.Mathf.Clamp(
                trigger,
                0f,
                sourceDuration);
        }
    }
}
