using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Orike.HTN
{
    /// <summary>
    /// HTN Agent：把定义域、世界状态、感知器、规划器和计划执行器
    /// 组装成一个会“自己决定做什么”的 NPC。
    ///
    /// 每帧流程：
    ///   1. 感知器采样更新世界状态（外部世界变化 → 标记需要重新规划）；
    ///   2. 需要时重新规划（计划完成 / 失败 / 世界状态变化）；
    ///   3. 计划校验（第 12.5 节）：用工作世界状态模拟剩余任务的
    ///      条件与效果，发现剩余计划失效立即重新规划；
    ///   4. 驱动当前基元任务的操作函数；成功后把普通效果施加到
    ///      真实世界状态并推进到下一个任务。
    ///
    /// MTR 优先级（第 12.8 节）：UseMtrPriority 打开时，重规划会带上
    /// 当前计划的 MTR 约束，保证新计划不会打断仍然合法的当前行为。
    /// </summary>
    public class HTNBrain : MonoBehaviour
    {
        [Header("定义域")]
        public HTNDomain Domain;

        [Header("规划")]
        [Tooltip("打开后重规划受当前计划 MTR 约束，只接受优先级相同或更高的新计划")]
        public bool UseMtrPriority = true;

        [Tooltip("规划 / 任务切换时输出日志")]
        public bool VerboseLog;

        [Tooltip("重新规划的最小间隔（秒）。规划失败时也不会每帧空转重试。")]
        public float ReplanCooldown = 0.25f;

        [Header("感知器（留空则自动收集子物体）")]
        public List<HTNSensor> Sensors =
            new List<HTNSensor>();


        /// <summary>新计划生成事件（调试 UI / 编辑器高亮用）。</summary>
        public event Action<HTNPlan> PlanCreated;

        /// <summary>当前计划被放弃事件（参数：放弃原因）。</summary>
        public event Action<string> PlanAborted;


        private HTNWorldState _worldState;

        private readonly HTNPlanner _planner =
            new HTNPlanner();

        private HTNPlan _plan;

        private int _planIndex;

        private bool _needReplan = true;

        /// <summary>距下次允许规划的时间（秒）。</summary>
        private float _replanCooldownLeft;

        private readonly HTNOperatorContext _operatorContext =
            new HTNOperatorContext();

        private readonly Dictionary<string, HTNOperator> _operatorCache =
            new Dictionary<string, HTNOperator>();


        // =========================================================
        // 生命周期
        // =========================================================

        private void Awake()
        {
            _operatorContext.Brain = this;
            _operatorContext.GameObject = gameObject;

            if (Domain != null)
            {
                _worldState = Domain.CreateWorldState();
            }
        }


        private void Start()
        {
            CollectSensorsIfNeeded();

            RequestReplan("初始规划");
        }


        private void Update()
        {
            if (Domain == null)
            {
                return;
            }

            SenseWorld();

            _replanCooldownLeft -= Time.deltaTime;

            if (_needReplan &&
                _replanCooldownLeft <= 0f)
            {
                Replan();
            }

            if (_plan == null ||
                _planIndex >= _plan.Tasks.Count)
            {
                // 计划已走完：下轮请求新计划
                if (_plan != null)
                {
                    AbandonPlan("计划执行完毕");
                }

                _needReplan = true;

                return;
            }

            // 计划校验：剩余任务条件不再满足 → 重新规划（第 12.5 节）
            if (!ValidateRemainingPlan())
            {
                AbandonPlan("剩余计划与当前世界状态冲突");

                _needReplan = true;

                return;
            }

            TickCurrentTask();
        }


        // =========================================================
        // 感知
        // =========================================================

        private void SenseWorld()
        {
            if (_worldState == null)
            {
                _worldState = Domain.CreateWorldState();
            }

            foreach (HTNSensor sensor in Sensors)
            {
                if (sensor == null)
                {
                    continue;
                }

                if (sensor.Sense(this, _worldState))
                {
                    // 世界状态变化 → 触发重新规划（第 12.4 节触发条件之一）
                    _needReplan = true;
                }
            }
        }


        private void CollectSensorsIfNeeded()
        {
            if (Sensors.Count > 0)
            {
                return;
            }

            Sensors.AddRange(
                GetComponentsInChildren<HTNSensor>());
        }


        // =========================================================
        // 规划
        // =========================================================

        /// <summary>请求在下一帧重新规划（供外部系统调用）。</summary>
        public void RequestReplan(string reason)
        {
            if (VerboseLog && !string.IsNullOrEmpty(reason))
            {
                Debug.Log(
                    $"[HTN] {name} 请求重新规划：{reason}",
                    this);
            }

            _needReplan = true;
        }


        private void Replan()
        {
            _needReplan = false;

            List<HTNPlanLogEntry> log =
                VerboseLog ? new List<HTNPlanLogEntry>() : null;

            HTNPlanner planner = _planner;

            HTNPlan newPlan;

            if (UseMtrPriority &&
                _plan != null &&
                _planIndex < _plan.Tasks.Count)
            {
                // 带当前计划的 MTR 约束规划：只接受优先级相同或更高的计划
                newPlan = planner.Plan(
                    Domain,
                    _worldState,
                    log,
                    _plan);

                if (!newPlan.Success)
                {
                    // MTR 约束下无解 → 放弃约束再试一次，
                    // 当前计划已失效时至少要给出一个替代行为
                    newPlan = planner.Plan(
                        Domain,
                        _worldState,
                        log);
                }
            }
            else
            {
                newPlan = planner.Plan(
                    Domain,
                    _worldState,
                    log);
            }


            // 勾选 VerboseLog 时输出逐步分解过程
            //（方法选中 / 基元任务加入 / 条件被拒 / 回溯）；
            // MTR 约束失败放宽重试时，两次尝试的过程都会在日志里
            if (VerboseLog &&
                log != null &&
                log.Count > 0)
            {
                StringBuilder sb =
                    new StringBuilder(
                        $"[HTN] {name} 本次规划分解过程（{log.Count} 步）：");

                for (int i = 0; i < log.Count; i++)
                {
                    sb.AppendLine();

                    sb.Append("  ")
                        .Append(i + 1)
                        .Append(". ")
                        .Append(log[i].Message);
                }

                Debug.Log(
                    sb.ToString(),
                    this);
            }


            if (newPlan.Success &&
                newPlan.Tasks.Count > 0)
            {
                _plan = newPlan;
                _planIndex = 0;

                PlanCreated?.Invoke(newPlan);

                if (VerboseLog)
                {
                    Debug.Log(
                        $"[HTN] {name} 新计划：{newPlan.Describe()}",
                        this);
                }
            }
            else
            {
                AbandonPlan(
                    string.IsNullOrEmpty(newPlan.FailReason)
                        ? "规划失败"
                        : newPlan.FailReason);

                _plan = null;

                // 失败后进入冷却，避免空域 / 错误域每帧空转重试
                _replanCooldownLeft =
                    Mathf.Max(
                        ReplanCooldown,
                        0f);

                if (VerboseLog)
                {
                    Debug.LogWarning(
                        $"[HTN] {name} 规划失败：{newPlan.FailReason}",
                        this);
                }
            }
        }


        private void AbandonPlan(string reason)
        {
            PlanAborted?.Invoke(reason);

            if (VerboseLog)
            {
                Debug.Log(
                    $"[HTN] {name} 放弃当前计划：{reason}",
                    this);
            }
        }


        // =========================================================
        // 计划校验（第 12.5 节）
        // =========================================================

        /// <summary>
        /// 用工作世界状态检查当前任务及其后所有任务的预设条件：
        /// 每检查一个任务就把它的效果（含期望效果）施加到工作状态，
        /// 保证后续条件的校验考虑了前置任务的叠加影响。
        /// </summary>
        private bool ValidateRemainingPlan()
        {
            HTNWorldState checkState = _worldState.Clone();

            for (int i = _planIndex; i < _plan.Tasks.Count; i++)
            {
                HTNPrimitiveTask task = _plan.Tasks[i];

                if (task == null)
                {
                    return false;
                }

                if (!task.AreConditionsSatisfied(checkState))
                {
                    return false;
                }

                task.ApplyEffects(
                    checkState,
                    true);
            }

            return true;
        }


        // =========================================================
        // 执行
        // =========================================================

        private void TickCurrentTask()
        {
            HTNPrimitiveTask task = _plan.Tasks[_planIndex];

            if (task == null)
            {
                AbandonPlan("计划中存在丢失的任务引用");

                _needReplan = true;

                return;
            }

            HTNOperator instance =
                ResolveOperator(task.OperatorId);

            if (instance == null)
            {
                Debug.LogError(
                    $"[HTN] {name} 任务 {task.name} 的操作函数 “{task.OperatorId}” 未注册",
                    this);

                AbandonPlan("操作函数未注册");

                _needReplan = true;

                return;
            }


            // 换了任务 → 清空操作函数的私有状态
            if (_operatorContext.Task != task)
            {
                _operatorContext.Task = task;
                _operatorContext.OperatorState = null;
            }

            _operatorContext.DeltaTime = Time.deltaTime;

            HTNOperatorStatus status =
                instance.Update(_operatorContext);

            switch (status)
            {
                case HTNOperatorStatus.Success:
                {
                    // 成功完成：普通效果施加到真实世界状态
                    task.ApplyEffects(_worldState);

                    _planIndex++;

                    break;
                }

                case HTNOperatorStatus.Failure:
                {
                    AbandonPlan($"任务 {task.name} 执行失败");

                    _needReplan = true;

                    break;
                }
            }
        }


        private HTNOperator ResolveOperator(string operatorId)
        {
            if (string.IsNullOrEmpty(operatorId))
            {
                return null;
            }

            if (_operatorCache.TryGetValue(operatorId, out HTNOperator cached))
            {
                return cached;
            }

            HTNOperator instance =
                HTNOperatorRegistry.Get(operatorId);

            _operatorCache[operatorId] = instance;

            return instance;
        }


        // =========================================================
        // 查询（调试 / 编辑器）
        // =========================================================

        /// <summary>当前真实世界状态（只读用途：调试面板、自定义感知器）。</summary>
        public HTNWorldState WorldState => _worldState;

        /// <summary>当前运行中的计划；没有计划时返回 null。</summary>
        public HTNPlan CurrentPlan => _plan;

        /// <summary>计划执行进度（已完成的基元任务数量）。</summary>
        public int PlanIndex => _planIndex;
    }
}
