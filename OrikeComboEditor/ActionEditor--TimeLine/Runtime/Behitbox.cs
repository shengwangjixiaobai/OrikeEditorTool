using UnityEngine;

/// <summary>
/// Behitbox（受击框）
///
/// 挂载在受击判定 GameObject 上
/// 配合 Unity 自带 Collider（建议 isTrigger=true）
///
/// 逻辑与 Hitbox 完全一致（继承 Hitbox）
/// 区别仅在语义：
///   - Hitbox   = 攻击方，主动产生伤害判定
///   - Behitbox = 受击方，被命中时接收判定
///
/// 战斗系统可以：
///   - GetComponent<Hitbox>()    统一处理攻击 / 受击
///   - GetComponent<Behitbox>()  仅取受击框
///
/// 默认 Gizmo 颜色为紫色
/// </summary>
public class Behitbox : Hitbox
{
    /// <summary>
    /// 组件首次添加 / Reset 时调用（编辑器）
    /// 把默认 Gizmo 颜色设为紫色
    /// </summary>
    private void Reset()
    {
        gizmoColor =
            new Color(
                0.65f,
                0.25f,
                0.85f,
                0.4f);
    }


    /// <summary>
    /// 运行时 / 实例化时确保默认紫色
    /// </summary>
    private void Awake()
    {
        gizmoColor =
            new Color(
                0.65f,
                0.25f,
                0.85f,
                0.4f);
    }
}
