using System;
using UnityEngine;

/// <summary>
/// Hitbox Clip
///
/// 引用一个挂载 Hitbox 脚本 + Collider 的 GameObject
/// 在 Clip 时间段内激活该 hitbox
/// </summary>
[Serializable]
public class HitboxClipData
    : BaseClipData
{
    public GameObject HitboxPrefab;

    /// <summary>
    /// 挂载骨骼名
    ///
    /// 空字符串 = 挂载到角色根
    /// 否则递归查找角色 Transform 树
    /// </summary>
    public string AttachBone;

    /// <summary>
    /// 相对挂载点的局部偏移
    /// </summary>
    public Vector3 LocalOffset;


    /// <summary>
    /// 当前 Clip 类型
    /// </summary>
    public override ClipType Type
    {
        get
        {
            return ClipType.Hitbox;
        }
    }


    /// <summary>
    /// 刷新数据
    ///
    /// Hitbox 没有原生时长概念
    /// 默认 0.5s，由用户在 Inspector 调整
    /// </summary>
    public override void RefreshData()
    {
        if (Length <= 0f)
        {
            Length = 0.5f;
        }

        EndTime =
            StartTime +
            Length;
    }


    /// <summary>
    /// 获取预览数据
    ///
    /// 在时间段内返回 HitboxPreviewData
    /// 时间段外返回 null
    /// </summary>
    public override BasePreviewData
        GetPreviewDataAtTime(
            float actionTime)
    {
        if (HitboxPrefab == null)
        {
            return null;
        }

        if (!ContainsTime(actionTime))
        {
            return null;
        }

        return new HitboxPreviewData(
            HitboxPrefab,
            AttachBone,
            LocalOffset,
            Type);
    }
}
