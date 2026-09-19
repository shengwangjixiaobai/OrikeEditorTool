using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 第三人称跟随 + 轨道旋转相机。
/// 挂在相机物体上，自动跟随目标（玩家），用鼠标 / 手柄右摇杆旋转视角，
/// 滚轮调整距离，并用 SphereCast 检测防止相机穿墙。
/// 输入复用 <see cref="InputManager"/> 的 CameraLook 动作。
/// </summary>
public class ThirdPersonCameraController : MonoBehaviour
{
    [Header("目标")]
    [Tooltip("跟随的目标；留空则自动按 targetTag 查找")]
    public Transform target;
    [Tooltip("自动查找目标时使用的标签")]
    public string targetTag = "Player";
    [Tooltip("注视点相对目标锚点的偏移（通常为胸口 / 头部高度）")]
    public Vector3 pivotOffset = new Vector3(0f, 1.6f, 0f);

    [Header("跟随")]
    [Tooltip("跟随平滑速度，越大越跟手")]
    [Range(1f, 30f)]
    public float followSpeed = 12f;

    [Header("旋转")]
    [Tooltip("鼠标灵敏度（Mouse/delta 的像素增量直接乘该系数）")]
    public float mouseSensitivity = 0.1f;
    [Tooltip("手柄右摇杆灵敏度（度 / 秒）")]
    public float gamepadSensitivity = 120f;
    [Tooltip("是否反转垂直方向")]
    public bool invertY = false;
    [Tooltip("俯仰角下限（向下看）")]
    public float minPitch = -30f;
    [Tooltip("俯仰角上限（向上看）")]
    public float maxPitch = 60f;

    [Header("距离")]
    [Range(0.5f, 30f)]
    public float distance = 5f;
    [Range(0.5f, 30f)]
    public float minDistance = 1.5f;
    [Range(0.5f, 30f)]
    public float maxDistance = 12f;
    [Tooltip("滚轮缩放速度")]
    public float zoomSpeed = 2f;

    [Header("碰撞")]
    [Tooltip("是否做穿墙检测，避免相机插入墙体")]
    public bool avoidObstacle = true;
    public LayerMask collisionMask = ~0;
    public float collisionRadius = 0.3f;
    [Range(1f, 30f)]
    public float collisionSmoothSpeed = 12f;

    [Header("光标")]
    [Tooltip("启用后锁定并隐藏鼠标光标")]
    public bool lockCursor = true;

    private float _yaw;
    private float _pitch = 15f;
    private float _targetDistance;
    private float _currentDistance;
    private Vector3 _smoothPivot;
    private bool _initialized;

    private void Awake()
    {
        _targetDistance = distance;
        _currentDistance = distance;
    }

    private void OnEnable()
    {
        SetCursorLock(lockCursor);
    }

    private void OnDisable()
    {
        SetCursorLock(false);
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            if (!TryFindTarget())
            {
                return;
            }
        }

        HandleRotation();
        HandleZoom();

        Vector3 pivot = target.TransformPoint(pivotOffset);
        if (!_initialized)
        {
            _smoothPivot = pivot;
            _initialized = true;
        }

        // 帧率无关的指数平滑跟随
        _smoothPivot = Vector3.Lerp(
            _smoothPivot,
            pivot,
            1f - Mathf.Exp(-followSpeed * Time.deltaTime));

        // 目标距离向配置距离平滑，再由碰撞检测收敛
        _currentDistance = Mathf.Lerp(
            _currentDistance,
            _targetDistance,
            1f - Mathf.Exp(-collisionSmoothSpeed * Time.deltaTime));

        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 direction = rotation * Vector3.back;
        float finalDistance = _currentDistance;

        if (avoidObstacle)
        {
            if (Physics.SphereCast(
                    _smoothPivot,
                    collisionRadius,
                    direction,
                    out RaycastHit hit,
                    _currentDistance,
                    collisionMask,
                    QueryTriggerInteraction.Ignore))
            {
                finalDistance = Mathf.Max(collisionRadius, hit.distance);
            }
        }

        transform.position = _smoothPivot + direction * finalDistance;
        transform.rotation = rotation;
    }

    private void HandleRotation()
    {
        Vector2 look = GetLookInput();
        _yaw += look.x;
        _pitch += look.y * (invertY ? 1f : -1f);
        _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
    }

    private void HandleZoom()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            _targetDistance = Mathf.Clamp(
                _targetDistance - scroll * zoomSpeed,
                minDistance,
                maxDistance);
        }
    }

    /// <summary>
    /// 读取并缩放视角输入。鼠标为像素增量，手柄右摇杆为 -1..1 的连续值，
    /// 两者量纲不同，需要分开缩放。
    /// </summary>
    private Vector2 GetLookInput()
    {
        if (InputManager.Instance == null)
        {
            return Vector2.zero;
        }

        Vector2 raw = InputManager.Instance.CameraLook_Value;

        InputDevice device = InputManager.Instance.CameraLook.activeControl?.device;
        float scale = device is Gamepad
            ? gamepadSensitivity * Time.deltaTime
            : mouseSensitivity;

        return new Vector2(raw.x * scale, raw.y * scale);
    }

    private bool TryFindTarget()
    {
        GameObject go = GameObject.FindWithTag(targetTag);
        if (go != null)
        {
            target = go.transform;
        }
        return target != null;
    }

    private static void SetCursorLock(bool locked)
    {
        Cursor.lockState = locked
            ? CursorLockMode.Locked
            : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}