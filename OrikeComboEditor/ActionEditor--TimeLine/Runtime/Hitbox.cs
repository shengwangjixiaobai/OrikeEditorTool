using UnityEngine;

/// <summary>
/// Hitbox / Behitbox 通用脚本
///
/// 挂载在 hitbox GameObject 上
/// 配合 Unity 自带 Collider（建议 isTrigger=true 无物理碰撞）
///
/// activate 字段：
///   - 编辑器预览时：由 ActionPreviewSystem 根据 Clip 时间段开关
///     同时控制 collider.enabled 用于可视化
///
///   - 游戏运行时：collider 始终 enabled（用于物理检测）
///     activate 由战斗系统 / 动作事件控制
///     实际判定时只看 activate，不看 collider.enabled
/// </summary>
public class Hitbox : MonoBehaviour
{
    /// <summary>
    /// 当前 hitbox 是否激活
    ///
    /// true  = 生效，可被判定
    /// false = 不生效
    /// </summary>
    public bool activate = true;

    /// <summary>
    /// Gizmo 显示颜色
    ///
    /// 编辑器预览时由 ActionPreviewSystem 设置
    /// （Hitbox 红色，Behitbox 紫色）
    ///
    /// 游戏中可由战斗系统自定义
    /// </summary>
    public Color gizmoColor =
        new Color(
            1f,
            0.25f,
            0.25f,
            0.4f);
}
