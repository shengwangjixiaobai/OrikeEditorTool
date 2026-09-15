using System;
using System.Collections.Generic;
using UnityEngine;

namespace Orike.ActionGraph
{
    /// <summary>
    /// 运行时动作控制核心。
    ///
    /// 职责：
    ///   - 当前 Action 管理
    ///   - Reservation 预约列表维护与过期清理
    ///   - KeyCommand 输入检测并生成预约
    ///   - Cancel / BeCancel 的 Tag 匹配与 Cancel Window 判断
    ///   - 最终优先级竞争，选择最优预约动作
    ///   - 执行 Transition（调用 ActionPlayer 播放目标 ActionData）
    ///   - 自动转移：非 Loop 动作到达 Exit Time 后自动 CrossFade 到目标
    ///
    /// 切换流程：
    ///   输入 -> KeyCommand 检测 -> 创建 Reservation ->
    ///   取消窗口检查 -> Tag 匹配 -> 优先级选择 ->
    ///   Transition -> ActionPlayer 播放。
    /// </summary>
    public class ActionController : MonoBehaviour
    {
        // =========================================================
        // 配置
        // =========================================================

        [Tooltip("动作关系图数据")]
        [SerializeField]
        private ActionGraphData graph;

        [Tooltip("动作播放器；为空时自动取同物体上的 ActionPlayer")]
        [SerializeField]
        private ActionPlayer player;

        [Tooltip("搓招输入检测；为空时自动查找场景中的 InputToCommand")]
        [SerializeField]
        private InputToCommand input;

        [Tooltip("动作自然播放结束后回到的默认动作（如 Idle）")]
        [SerializeField]
        private Action defaultAction;

        [Tooltip("启动时是否自动播放默认动作")]
        [SerializeField]
        private bool playDefaultOnStart =
            true;

        [Tooltip("预约有效时长（秒）：输入提前量的保留时间")]
        [SerializeField]
        private float reservationDuration =
            0.5f;

        [Tooltip("每帧自动扫描输入生成预约")]
        [SerializeField]
        private bool autoScanInput =
            true;

        [Tooltip("触发后防止按键长按重复触发的冷却时间（秒）")]
        [SerializeField]
        private float inputRepeatCooldown =
            0.2f;


        // =========================================================
        // 运行时状态
        // =========================================================

        private readonly List<ReservationAction> _reservations =
            new List<ReservationAction>();

        private readonly Dictionary<Action, float> _lastConsumeTime =
            new Dictionary<Action, float>();

        private Action _current;


        // =========================================================
        // 事件
        // =========================================================

        /// <summary>
        /// 当前动作切换：参数依次为 from / to。
        /// 注意：必须使用 System.Action 全名，
        /// 否则会解析到同命名空间内非泛型的 Action 逻辑类。
        /// </summary>
        public event System.Action<Action, Action> ActionChanged;


        // =========================================================
        // 属性
        // =========================================================

        public ActionGraphData Graph
        {
            get => graph;
            set => graph = value;
        }

        public ActionPlayer Player
        {
            get => player;
            set => player = value;
        }

        public InputToCommand Input
        {
            get => input;
            set => input = value;
        }

        public Action DefaultAction
        {
            get => defaultAction;
            set => defaultAction = value;
        }

        /// <summary>
        /// 当前正在播放的逻辑动作。
        /// </summary>
        public Action Current =>
            _current;

        /// <summary>
        /// 当前动作的播放时间（秒），来自 ActionPlayer。
        /// </summary>
        public float CurrentActionTime =>
            player != null
                ? player.CurrentTime
                : 0f;

        /// <summary>
        /// 当前预约列表（只读）。
        /// </summary>
        public IReadOnlyList<ReservationAction> Reservations =>
            _reservations;


        // =========================================================
        // Unity
        // =========================================================

        private void Awake()
        {
            if (player == null)
            {
                player =
                    GetComponent<ActionPlayer>();
            }

            if (input == null)
            {
                input =
                    FindAnyObjectByType<InputToCommand>();
            }
        }


        private void Start()
        {
            if (graph != null)
            {
                graph.RefreshData();
            }

            if (playDefaultOnStart &&
                defaultAction != null)
            {
                PlayImmediate(defaultAction);
            }
        }


        private void OnEnable()
        {
            if (player != null)
            {
                player.ActionCompleted +=
                    HandleActionCompleted;
            }
        }


        private void OnDisable()
        {
            if (player != null)
            {
                player.ActionCompleted -=
                    HandleActionCompleted;
            }
        }


        private void Update()
        {
            Tick();
        }


        // =========================================================
        // 每帧驱动
        // =========================================================

        /// <summary>
        /// 一帧完整流程：扫描输入 -> 清理过期预约 -> 尝试执行 ->
        /// 自动转移（Exit Time 轮询）。
        /// 也可在非自动模式下由外部手动调用。
        /// </summary>
        public void Tick()
        {
            if (graph == null ||
                player == null)
            {
                return;
            }

            if (autoScanInput)
            {
                ScanInput();
            }

            RemoveExpiredReservations();

            TryExecuteReservations();

            // 自动转移放在最后：同帧有输入取消时输入优先
            TryAutoTransition();
        }


        // =========================================================
        // 输入扫描
        // =========================================================

        /// <summary>
        /// 遍历图中全部 Action，
        /// 只要任意一个 KeyCommand 在本帧搓招成功，就预约该 Action。
        /// </summary>
        public void ScanInput()
        {
            if (input == null ||
                graph == null)
            {
                return;
            }

            foreach (Action action in graph.Actions)
            {
                if (action == null ||
                    action == _current ||
                    action.KeyCommands == null ||
                    action.KeyCommands.Count == 0)
                {
                    continue;
                }

                if (IsInRepeatCooldown(action))
                {
                    continue;
                }

                foreach (KeyCommand command in action.KeyCommands)
                {
                    if (command == null ||
                        command.key == null ||
                        command.key.Length == 0)
                    {
                        continue;
                    }

                    if (input.occurCommand(command))
                    {
                        Reserve(action);

                        break;
                    }
                }
            }
        }


        private bool IsInRepeatCooldown(
            Action action)
        {
            if (inputRepeatCooldown <= 0f)
            {
                return false;
            }

            if (_lastConsumeTime.TryGetValue(
                    action,
                    out float lastTime))
            {
                return
                    Time.time -
                    lastTime <
                    inputRepeatCooldown;
            }

            return false;
        }


        // =========================================================
        // 预约
        // =========================================================

        /// <summary>
        /// 预约一个目标动作（外部代码 / 输入扫描均可调用）。
        /// 已存在相同目标的预约时不重复添加。
        /// </summary>
        public ReservationAction Reserve(
            Action target)
        {
            if (target == null ||
                graph == null)
            {
                return null;
            }

            foreach (ReservationAction existing in _reservations)
            {
                if (existing != null &&
                    existing.Target == target)
                {
                    return existing;
                }
            }

            TransitionData transition =
                graph.GetTransition(
                    _current,
                    target);

            ReservationAction reservation =
                ReservationAction.Create(
                    target,
                    transition,
                    Time.time,
                    reservationDuration);

            _reservations.Add(reservation);

            return reservation;
        }


        /// <summary>
        /// 清空全部预约。
        /// </summary>
        public void ClearReservations()
        {
            _reservations.Clear();
        }


        private void RemoveExpiredReservations()
        {
            for (int i = _reservations.Count - 1;
                 i >= 0;
                 i--)
            {
                if (_reservations[i].IsExpired(
                        Time.time))
                {
                    _reservations.RemoveAt(i);
                }
            }
        }


        // =========================================================
        // 取消匹配与优先级竞争
        // =========================================================

        /// <summary>
        /// 判断在当前动作时间下，预约动作是否具备取消资格。
        ///
        /// 条件：
        ///   1. 目标动作拥有当前动作某个 BeCancel 的 Tag；
        ///   2. 当前播放时间处于该 BeCancel 窗口内。
        /// </summary>
        public bool CanExecuteNow(
            ReservationAction reservation)
        {
            if (reservation == null ||
                reservation.Target == null)
            {
                return false;
            }

            Action target =
                reservation.Target;

            // 没有当前动作时，任何预约都可以直接执行
            if (_current == null)
            {
                return true;
            }

            if (target == _current)
            {
                return false;
            }

            float actionTime =
                CurrentActionTime;

            foreach (BeCancelData beCancel in _current.BeCancels)
            {
                if (beCancel == null ||
                    string.IsNullOrEmpty(beCancel.Tag))
                {
                    continue;
                }

                if (!target.HasCancelTag(beCancel.Tag))
                {
                    continue;
                }

                if (beCancel.ContainsTime(actionTime))
                {
                    return true;
                }
            }

            return false;
        }


        /// <summary>
        /// 从预约列表中选出当前可以执行、
        /// 且最终优先级最高的预约。
        /// 同优先级时后预约者优先（更新的输入意图）。
        /// </summary>
        private ReservationAction SelectBestReservation()
        {
            ReservationAction best =
                null;

            foreach (ReservationAction reservation in _reservations)
            {
                if (!CanExecuteNow(reservation))
                {
                    continue;
                }

                if (best == null)
                {
                    best =
                        reservation;

                    continue;
                }

                if (reservation.FinalPriority >
                    best.FinalPriority)
                {
                    best =
                        reservation;
                }
                else if (reservation.FinalPriority ==
                         best.FinalPriority &&
                         reservation.CreateTime >
                         best.CreateTime)
                {
                    best =
                        reservation;
                }
            }

            return best;
        }


        private void TryExecuteReservations()
        {
            if (_reservations.Count == 0)
            {
                return;
            }

            ReservationAction best =
                SelectBestReservation();

            if (best != null)
            {
                Execute(best);
            }
        }


        // =========================================================
        // 执行切换
        // =========================================================

        /// <summary>
        /// 立即执行指定动作（无视预约与取消窗口），用于默认动作 / 外部强制切换。
        /// </summary>
        public void PlayImmediate(
            Action target)
        {
            if (target == null ||
                player == null)
            {
                return;
            }

            if (target.ActionData == null)
            {
                Debug.LogWarning(
                    $"[ActionController] Action \"{target.Id}\" 未绑定 ActionData，无法播放。");

                return;
            }

            ClearReservations();

            player.Play(
                target.ActionData,
                target.Loop,
                0f);

            SwitchCurrent(target);
        }


        /// <summary>
        /// 立即尝试切换到目标动作：
        /// 创建预约并立刻做一次取消判断（供按钮 / 单帧测试使用）。
        /// </summary>
        public bool TryStartAction(
            Action target)
        {
            ReservationAction reservation =
                Reserve(target);

            if (reservation == null)
            {
                return false;
            }

            if (!CanExecuteNow(reservation))
            {
                return false;
            }

            Execute(reservation);

            return true;
        }


        private void Execute(
            ReservationAction reservation)
        {
            Action target =
                reservation.Target;

            if (target.ActionData == null)
            {
                Debug.LogWarning(
                    $"[ActionController] Action \"{target.Id}\" 未绑定 ActionData，取消切换。");

                _reservations.Remove(reservation);

                return;
            }

            TransitionData transition =
                reservation.Transition;

            // ActionPlayer：按 Transition 参数混合到目标 ActionData
            if (_current == null)
            {
                player.Play(
                    target.ActionData,
                    target.Loop,
                    GetImmediateStartPercent(
                        target,
                        transition));
            }
            else
            {
                PlayTransition(
                    target,
                    transition);
            }


            // 切换后旧预约全部失效（动作上下文已变化）
            _reservations.Clear();

            _lastConsumeTime[target] =
                Time.time;

            SwitchCurrent(target);
        }


        /// <summary>
        /// 按 Transition 配置把目标动作交给播放器：
        /// UseFixedTime 走 CrossFadeInFixedTime（秒），
        /// 否则走 CrossFade（归一化，时长相对来源动作）。
        /// </summary>
        private void PlayTransition(
            Action target,
            TransitionData transition)
        {
            if (transition == null)
            {
                player.CrossFade(
                    target.ActionData,
                    -1f,
                    target.Loop);

                return;
            }

            if (transition.UseFixedTime)
            {
                player.CrossFadeInFixedTime(
                    target.ActionData,
                    transition.TransitionDuration,
                    target.Loop,
                    transition.TimeOffset,
                    transition.TransitionTime);
            }
            else
            {
                player.CrossFade(
                    target.ActionData,
                    transition.TransitionDuration,
                    target.Loop,
                    transition.TimeOffset,
                    transition.TransitionTime);
            }
        }


        /// <summary>
        /// 没有来源动作（直接 Play）时，
        /// 把 Transition 的目标起点换算成 Play 所需的归一化值。
        /// </summary>
        private static float GetImmediateStartPercent(
            Action target,
            TransitionData transition)
        {
            if (transition == null)
            {
                return 0f;
            }

            if (!transition.UseFixedTime)
            {
                return transition.TimeOffset;
            }

            float duration =
                ActionDataUtility.GetDuration(
                    target.ActionData);

            return duration > 0f
                ? Mathf.Clamp01(
                    transition.TimeOffset /
                    duration)
                : 0f;
        }


        private void SwitchCurrent(
            Action target)
        {
            Action from =
                _current;

            if (from == target)
            {
                return;
            }

            _current =
                target;

            ActionChanged?.Invoke(
                from,
                target);
        }


        // =========================================================
        // 播放完成 / 自动转移
        // =========================================================

        private void HandleActionCompleted(
            ActionData completedData)
        {
            // CrossFade 中途被挤掉的旧动作不会触发完成事件，
            // 这里只处理当前动作自然播放结束的情况。
            if (_current == null ||
                completedData != _current.ActionData)
            {
                return;
            }

            // 非 Loop 动作：存在自动转移连线时播完即切换
            // （兜底路径，正常情况下 Exit Time 轮询会提前触发过渡）
            if (!_current.Loop &&
                HasValidAutoTransition(
                    out TransitionData transition))
            {
                ExecuteAutoTransition(
                    transition);

                return;
            }

            if (defaultAction != null &&
                defaultAction != _current)
            {
                PlayImmediate(defaultAction);
            }
        }


        /// <summary>
        /// 是否存在有效的“结束自动转移”连线：
        /// 有连线、目标非空且不是自己、目标已绑定 ActionData。
        /// </summary>
        private bool HasValidAutoTransition(
            out TransitionData transition)
        {
            transition =
                _current != null && graph != null
                    ? graph.GetAutoTransition(
                        _current)
                    : null;

            return
                transition != null &&
                transition.To != null &&
                transition.To != _current &&
                transition.To.ActionData != null;
        }


        /// <summary>
        /// 每帧轮询：当前非 Loop 动作播放到自动触发点时，
        /// 提前开始向目标动作 CrossFade。
        /// 触发点 = 来源时长 - 剩余混合时长，
        /// 保证过渡终点恰好落在来源动作播完时。
        /// </summary>
        private void TryAutoTransition()
        {
            if (_current == null ||
                _current.Loop ||
                !HasValidAutoTransition(
                    out TransitionData transition))
            {
                return;
            }

            if (CurrentActionTime <
                transition.GetAutoTriggerTime(
                    _current.ActionData))
            {
                return;
            }

            ExecuteAutoTransition(
                transition);
        }


        /// <summary>
        /// 按自动转移连线的参数混合到目标动作。
        /// </summary>
        private void ExecuteAutoTransition(
            TransitionData transition)
        {
            Action target =
                transition.To;

            ClearReservations();

            PlayTransition(
                target,
                transition);

            SwitchCurrent(
                target);
        }
    }
}
