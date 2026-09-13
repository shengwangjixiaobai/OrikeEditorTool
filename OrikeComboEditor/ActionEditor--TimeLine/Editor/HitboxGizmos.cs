using UnityEditor;
using UnityEngine;

/// <summary>
/// 为所有 Hitbox 组件绘制 Collider Gizmo
///
/// Unity 默认不为未选中的隐藏对象绘制 Collider Gizmo
/// 而编辑器预览的 hitbox 实例使用 HideFlags.HideAndDontSave
/// 不会出现在 Hierarchy 中
/// 所以这里用 [DrawGizmo] 主动绘制
/// </summary>
public static class HitboxGizmos
{
    [DrawGizmo(
        GizmoType.NonSelected |
        GizmoType.Selected)]
    private static void DrawGizmos(
        Hitbox hitbox,
        GizmoType gizmoType)
    {
        if (hitbox == null)
        {
            return;
        }

        Collider collider =
            hitbox.GetComponent<Collider>();

        if (collider == null)
        {
            return;
        }

        // =====================================================
        // 颜色
        // 激活时用 gizmoColor，未激活时用灰色
        // =====================================================

        Color fillColor;

        Color wireColor;

        if (hitbox.activate)
        {
            fillColor =
                hitbox.gizmoColor;

            wireColor =
                new Color(
                    hitbox.gizmoColor.r,
                    hitbox.gizmoColor.g,
                    hitbox.gizmoColor.b,
                    1f);
        }
        else
        {
            fillColor =
                new Color(
                    0.5f,
                    0.5f,
                    0.5f,
                    0.1f);

            wireColor =
                new Color(
                    0.5f,
                    0.5f,
                    0.5f,
                    0.4f);
        }

        Gizmos.color =
            fillColor;

        DrawColliderFill(
            collider);

        Gizmos.color =
            wireColor;

        DrawColliderWire(
            collider);
    }


    // =========================================================
    // Draw Collider Fill
    // =========================================================

    private static void DrawColliderFill(
        Collider collider)
    {
        if (collider is BoxCollider box)
        {
            Gizmos.matrix =
                box.transform
                    .localToWorldMatrix;

            Gizmos.DrawCube(
                box.center,
                box.size);

            return;
        }

        if (collider is SphereCollider sphere)
        {
            Gizmos.matrix =
                sphere.transform
                    .localToWorldMatrix;

            Gizmos.DrawSphere(
                sphere.center,
                sphere.radius);

            return;
        }

        if (collider is CapsuleCollider capsule)
        {
            DrawCapsule(
                capsule,
                true);

            return;
        }
    }


    // =========================================================
    // Draw Collider Wire
    // =========================================================

    private static void DrawColliderWire(
        Collider collider)
    {
        if (collider is BoxCollider box)
        {
            Gizmos.matrix =
                box.transform
                    .localToWorldMatrix;

            Gizmos.DrawWireCube(
                box.center,
                box.size);

            return;
        }

        if (collider is SphereCollider sphere)
        {
            Gizmos.matrix =
                sphere.transform
                    .localToWorldMatrix;

            Gizmos.DrawWireSphere(
                sphere.center,
                sphere.radius);

            return;
        }

        if (collider is CapsuleCollider capsule)
        {
            DrawCapsule(
                capsule,
                false);

            return;
        }
    }


    // =========================================================
    // Draw Capsule
    //
    // Unity Gizmos 没有原生 Capsule 绘制
    // 用两个球 + 一个圆柱近似
    // =========================================================

    private static void DrawCapsule(
        CapsuleCollider capsule,
        bool fill)
    {
        Gizmos.matrix =
            capsule.transform
                .localToWorldMatrix;

        float radius =
            capsule.radius;

        float height =
            capsule.height;

        Vector3 center =
            capsule.center;

        // 圆柱方向（X=0, Y=1, Z=2）
        Vector3 axis;

        Vector3 forward;

        switch (capsule.direction)
        {
            case 0:
                axis = Vector3.right;
                forward = Vector3.forward;
                break;

            case 1:
            default:
                axis = Vector3.up;
                forward = Vector3.right;
                break;

            case 2:
                axis = Vector3.forward;
                forward = Vector3.up;
                break;
        }

        float halfCylinder =
            Mathf.Max(
                0f,
                height * 0.5f -
                radius);

        Vector3 top =
            center + axis * halfCylinder;

        Vector3 bottom =
            center - axis * halfCylinder;

        if (fill)
        {
            Gizmos.DrawSphere(
                top,
                radius);

            Gizmos.DrawSphere(
                bottom,
                radius);
        }
        else
        {
            Gizmos.DrawWireSphere(
                top,
                radius);

            Gizmos.DrawWireSphere(
                bottom,
                radius);
        }

        // 圆柱用一个缩放的立方体近似
        Vector3 size;

        if (capsule.direction == 0)
        {
            size = new Vector3(
                halfCylinder * 2f,
                radius * 2f,
                radius * 2f);
        }
        else if (capsule.direction == 1)
        {
            size = new Vector3(
                radius * 2f,
                halfCylinder * 2f,
                radius * 2f);
        }
        else
        {
            size = new Vector3(
                radius * 2f,
                radius * 2f,
                halfCylinder * 2f);
        }

        if (fill)
        {
            Gizmos.DrawCube(
                center,
                size);
        }
        else
        {
            Gizmos.DrawWireCube(
                center,
                size);
        }
    }
}
