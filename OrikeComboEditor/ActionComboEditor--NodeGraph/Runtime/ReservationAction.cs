using UnityEngine;

namespace Orike.ActionGraph
{
    /// <summary>
    /// 预约动作。
    ///
    /// 输入检测成功后不会立即切换动作，
    /// 而是生成 ReservationAction 放入 ActionController 的预约列表，
    /// 用于解决输入提前量、多动作竞争与优先级判断。
    /// </summary>
    public class ReservationAction
    {
        /// <summary>
        /// 目标 Action。
        /// </summary>
        public Action Target;

        /// <summary>
        /// 当前动作到目标动作的 Transition；
        /// 图中没有配置连线时为 null（使用默认过渡）。
        /// </summary>
        public TransitionData Transition;

        /// <summary>
        /// Action 基础优先级。
        /// </summary>
        public int BasePriority;

        /// <summary>
        /// Transition 额外优先级。
        /// </summary>
        public int TransitionPriority;

        /// <summary>
        /// 创建预约的时间（Time.time）。
        /// </summary>
        public float CreateTime;

        /// <summary>
        /// 预约有效期（秒），超时自动丢弃。
        /// </summary>
        public float Duration;


        /// <summary>
        /// 最终优先级 = ActionPriority + TransitionPriority。
        /// </summary>
        public int FinalPriority =>
            BasePriority +
            TransitionPriority;


        /// <summary>
        /// 预约是否已过期。
        /// </summary>
        public bool IsExpired(
            float currentTime)
        {
            return
                currentTime -
                CreateTime >=
                Duration;
        }


        // =========================================================
        // 工厂
        // =========================================================

        public static ReservationAction Create(
            Action target,
            TransitionData transition,
            float currentTime,
            float duration)
        {
            ReservationAction reservation =
                new ReservationAction
                {
                    Target = target,

                    Transition = transition,

                    BasePriority =
                        target != null
                            ? target.Priority
                            : 0,

                    TransitionPriority =
                        transition != null
                            ? transition.Priority
                            : 0,

                    CreateTime =
                        currentTime,

                    Duration =
                        duration,
                };

            return reservation;
        }
    }
}
