using System;
using UnityEngine;

[Serializable]
public class EffectPreviewData
    : BasePreviewData
{
    /// <summary>
    /// 当前需要预览的特效预制体
    /// </summary>
    public GameObject EffectPrefab;

    /// <summary>
    /// 当前特效内部播放时间
    /// </summary>
    public float LocalTime;

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


    public EffectPreviewData(
        GameObject effectPrefab,
        float localTime,
        string attachBone,
        Vector3 localOffset)
    {
        EffectPrefab =
            effectPrefab;

        LocalTime =
            localTime;

        AttachBone =
            attachBone;

        LocalOffset =
            localOffset;
    }
}
