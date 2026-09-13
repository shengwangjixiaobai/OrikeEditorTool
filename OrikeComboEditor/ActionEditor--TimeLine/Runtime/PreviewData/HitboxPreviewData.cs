using System;
using UnityEngine;

[Serializable]
public class HitboxPreviewData
    : BasePreviewData
{
    /// <summary>
    /// 当前需要预览的 hitbox 预制体
    /// </summary>
    public GameObject HitboxPrefab;

    /// <summary>
    /// 挂载骨骼名
    /// </summary>
    public string AttachBone;

    /// <summary>
    /// 相对挂载点的局部偏移
    /// </summary>
    public Vector3 LocalOffset;

    /// <summary>
    /// 原始 Clip 类型
    ///
    /// 用于在预览时区分 Hitbox / Behitbox
    /// 设置不同的 Gizmo 颜色
    /// </summary>
    public ClipType ClipType;


    public HitboxPreviewData(
        GameObject hitboxPrefab,
        string attachBone,
        Vector3 localOffset,
        ClipType clipType)
    {
        HitboxPrefab =
            hitboxPrefab;

        AttachBone =
            attachBone;

        LocalOffset =
            localOffset;

        ClipType =
            clipType;
    }
}
