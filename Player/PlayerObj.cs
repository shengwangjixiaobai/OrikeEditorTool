using System.Collections.Generic;
using Orike.ActionGraph;
using UnityEngine;

/// <summary>
/// 玩家对象（MonoBehaviour）。
/// 读取移动输入，通过 <see cref="CharacterRotationController"/> 按摄像机相对方向
/// 转动角色朝向；仅当玩家处于指定动作时才允许旋转。
///
/// 根动画分工（防抖关键）：
///   - 位移：始终沿用根动画（OnAnimatorMove 里应用 deltaPosition）；
///   - 朝向：玩家转向时 100% 由脚本平滑控制（LateUpdate 应用），
///           忽略根动画的旋转增量；玩家不转向的动作
///           （如带转身根动画的攻击）保留根动画旋转。
/// 通过 OnAnimatorMove 接管后，Animator 不再做“动画根朝向 vs 实际朝向”的
/// 自动回正修正 —— 正是这个修正与脚本每帧写旋转互相拉扯，导致高频抖动。
/// </summary>
public class PlayerObj : MonoBehaviour
{
    [Header("摄像机")]
    [Tooltip("用于计算朝向的摄像机；留空则自动查找 MainCamera")]
    [SerializeField]
    private Transform _cameraTransform;

    [Header("旋转控制")]
    [SerializeField]
    private CharacterRotationController _rotation =
        new CharacterRotationController();

    [Header("旋转条件")]
    [Tooltip("仅当当前动作 Id 在此列表中时才允许旋转；留空表示始终允许旋转")]
    [SerializeField]
    private List<string> _rotateAllowedActions = new List<string>();

    [Tooltip("用于读取当前动作；留空则自动 GetComponent")]
    [SerializeField]
    private ActionController _actionController;

    [Header("根动画")]
    [Tooltip("同物体上的 Animator；留空则自动获取。用于 OnAnimatorMove 接管根动画")]
    [SerializeField]
    private Animator _animator;

    /// <summary>Update 里读取的移动输入，LateUpdate / OnAnimatorMove 使用。</summary>
    private Vector2 _moveInput;

    /// <summary>本帧是否由玩家输入控制朝向（Update 里算好，同帧的 OnAnimatorMove 可用）。</summary>
    private bool _steering;

    private void Awake()
    {
        if (_cameraTransform == null)
        {
            Camera camera = Camera.main;
            if (camera != null)
            {
                _cameraTransform = camera.transform;
            }
        }

        if (_actionController == null)
        {
            _actionController = GetComponent<ActionController>();
        }

        if (_animator == null)
        {
            _animator = GetComponent<Animator>();
        }

        if (_animator == null)
        {
            Debug.LogWarning(
                "[PlayerObj] 同物体上没有 Animator，" +
                "OnAnimatorMove 接管不会生效，根动画仍由引擎自动应用（可能抖动）",
                this);
        }
    }

    /// <summary>
    /// 每帧 Update 只做输入与状态计算（在 Animator 求值之前，
    /// 保证同帧的 OnAnimatorMove 能拿到最新意图）；
    /// 旋转在 LateUpdate 应用。
    /// </summary>
    private void Update()
    {
        _moveInput = InputManager.Instance != null
            ? InputManager.Instance.Move_Value
            : Vector2.zero;

        // 不在允许旋转的动作中时，视为无移动输入
        if (!CanRotateNow())
        {
            _moveInput = Vector2.zero;
        }

        _steering =
            _moveInput.sqrMagnitude > 0.0001f;
    }

    /// <summary>
    /// 接管根动画应用（实现本回调后，引擎不再自动应用根动画，
    /// 也不再做动画根朝向与实际朝向之间的回正修正）。
    /// </summary>
    private void OnAnimatorMove()
    {
        if (_animator == null)
        {
            return;
        }

        // 位移：沿用根动画的帧位移
        transform.position += _animator.deltaPosition;

        // 旋转：玩家正在转向时忽略根动画旋转增量，
        // 朝向由 LateUpdate 的平滑转向全权负责；
        // 玩家未转向的动作保留根动画旋转（如攻击的转身）
        if (!_steering)
        {
            transform.rotation *= _animator.deltaRotation;
        }
    }

    /// <summary>
    /// 旋转放在 LateUpdate 应用：Unity 每帧顺序为
    /// 脚本 Update → Animator 求值（含 OnAnimatorMove）→ 脚本 LateUpdate → 渲染，
    /// 保证脚本平滑出的朝向是本帧渲染前的最终朝向。
    /// </summary>
    private void LateUpdate()
    {
        if (_cameraTransform == null || _rotation == null)
        {
            return;
        }

        Vector3 targetDirection =
            _rotation.ComputeTargetDirection(_moveInput, _cameraTransform);

        transform.rotation = _rotation.RotateTowards(
            transform.rotation,
            targetDirection,
            Time.deltaTime);
    }

    /// <summary>
    /// 判断当前是否允许旋转：列表为空则始终允许；
    /// 否则要求当前动作 Id 在列表中。
    /// </summary>
    private bool CanRotateNow()
    {
        if (_rotateAllowedActions == null || _rotateAllowedActions.Count == 0)
        {
            return true;
        }

        if (_actionController == null || _actionController.Current == null)
        {
            return false;
        }

        return _rotateAllowedActions.Contains(_actionController.Current.Id);
    }
}
