using System;
using UnityEngine;

/// <summary>
/// 角色旋转控制（纯逻辑类，非 MonoBehaviour）。
/// 依据摄像机朝向与移动输入计算目标朝向（摄像机相对），并对偏航角做平滑转向。
///
/// 防抖动设计（配合 PlayerObj 在 LateUpdate 中应用旋转）：
///   1. 偏航角由本类自己维护（_yaw 是唯一状态源），每帧用 SmoothDampAngle
///      逼近目标角，不读取 transform 的实时旋转 —— 根动画的旋转增量不会
///      累积进转向状态，脚本旋转与根动画旋转不再互相拉扯；
///   2. 夹角进入死区时直接吸附到目标角并清零角速度，干净地结束本次转向，
///      避免“进入死区停止 → 目标漂出死区 → 从 0 重新加速”的极限环抖动；
///   3. 无移动输入时不写旋转，并回读角色当前朝向同步状态角：
///      根动画可以自由驱动朝向，恢复输入时从根动画留下的朝向无缝继续。
/// </summary>
[Serializable]
public class CharacterRotationController
{
    [Tooltip("转向平滑时间（秒），越大越平滑、越慢")]
    public float turnSmoothTime = 0.12f;

    [Tooltip("目标朝向与当前朝向夹角小于该值时直接吸附到目标角，过滤微小抖动")]
    public float deadZoneAngle = 1f;

    /// <summary>本类维护的当前偏航角（状态源，不受根动画影响）。</summary>
    private float _yaw;

    /// <summary>是否已从角色当前旋转初始化过 _yaw。</summary>
    private bool _hasYaw;

    private float _yawVelocity;

    /// <summary>
    /// 根据摄像机朝向与移动输入计算目标朝向（世界空间、已压平到水平面）。
    /// 移动输入为 0 时返回零向量，表示保持当前朝向。
    /// </summary>
    public Vector3 ComputeTargetDirection(Vector2 moveInput, Transform cameraTransform)
    {
        if (cameraTransform == null)
        {
            return Vector3.zero;
        }

        Vector3 cameraForward = cameraTransform.forward;
        cameraForward.y = 0f;
        cameraForward.Normalize();

        Vector3 cameraRight = cameraTransform.right;
        cameraRight.y = 0f;
        cameraRight.Normalize();

        Vector3 direction =
            cameraForward * moveInput.y +
            cameraRight * moveInput.x;

        if (direction.sqrMagnitude < 0.0001f)
        {
            return Vector3.zero;
        }

        return direction.normalized;
    }

    /// <summary>
    /// 对偏航角做平滑转向，返回本帧最终旋转。
    ///
    /// 偏航角始终基于本类维护的 _yaw 状态计算（而不是 transform 当前旋转），
    /// 因此根动画的旋转增量不会参与转向；无输入时返回原旋转、不写朝向。
    /// </summary>
    public Quaternion RotateTowards(Quaternion currentRotation, Vector3 targetDirection, float deltaTime)
    {
        // 无移动输入：不写旋转（根动画可以自由驱动朝向），
        // 并把状态角同步到角色当前朝向，恢复输入时从这继续
        if (targetDirection.sqrMagnitude < 0.0001f)
        {
            _yawVelocity = 0f;
            _yaw = currentRotation.eulerAngles.y;
            _hasYaw = true;

            return currentRotation;
        }

        // 首次有输入时，从角色当前朝向起步
        if (!_hasYaw)
        {
            _yaw = currentRotation.eulerAngles.y;
            _hasYaw = true;
        }

        float targetYaw = Mathf.Atan2(targetDirection.x, targetDirection.z) * Mathf.Rad2Deg;

        // 死区：剩余夹角很小 → 直接吸附到目标角并结束本次转向。
        // 不回传带根动画增量的原始旋转，也不保留旧角速度，
        // 消除跨死区边界时的来回拉扯与跳变
        if (Mathf.Abs(Mathf.DeltaAngle(_yaw, targetYaw)) < deadZoneAngle)
        {
            _yaw = targetYaw;
            _yawVelocity = 0f;

            return Quaternion.Euler(0f, _yaw, 0f);
        }

        _yaw = Mathf.SmoothDampAngle(
            _yaw,
            targetYaw,
            ref _yawVelocity,
            turnSmoothTime,
            Mathf.Infinity,
            deltaTime);

        return Quaternion.Euler(0f, _yaw, 0f);
    }
}
