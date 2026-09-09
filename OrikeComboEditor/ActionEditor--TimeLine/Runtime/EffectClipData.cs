using System;
using UnityEngine;

[Serializable]
public class EffectClipData
    : BaseClipData
{
    public GameObject EffectPrefab;

    /// <summary>
    /// 挂载骨骼名
    ///
    /// 空字符串 = 挂载到角色根
    /// 否则用递归查找角色 Transform 树中第一个同名节点
    ///
    /// 例如 "RightHand" / "Head" / "Spine02"
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
            return ClipType.Effect;
        }
    }


    /// <summary>
    /// 刷新 Effect 数据
    /// 取特效预制体上所有 ParticleSystem 的最长持续时间
    /// </summary>
    public override void RefreshData()
    {
        if (EffectPrefab == null)
        {
            Length = 0f;

            EndTime = StartTime;

            return;
        }

        float maxDuration =
            GetMaxParticleDuration(
                EffectPrefab);

        // 没有任何 ParticleSystem
        // 给一个默认长度，由用户在 Inspector 里调整
        if (maxDuration <= 0f)
        {
            maxDuration = 1f;
        }

        Length =
            maxDuration;

        EndTime =
            StartTime +
            Length;
    }


    public override BasePreviewData
        GetPreviewDataAtTime(
            float actionTime)
    {
        if (EffectPrefab == null)
        {
            return null;
        }

        if (!ContainsTime(actionTime))
        {
            return null;
        }

        float localTime =
            actionTime -
            StartTime;

        localTime =
            Mathf.Clamp(
                localTime,
                0f,
                Length);

        return new EffectPreviewData(
            EffectPrefab,
            localTime,
            AttachBone,
            LocalOffset);
    }


    // =========================================================
    // Get Max Particle Duration
    // =========================================================

    private static float
        GetMaxParticleDuration(
            GameObject prefab)
    {
        if (prefab == null)
        {
            return 0f;
        }

        ParticleSystem[] particles =
            prefab.GetComponentsInChildren<
                ParticleSystem>(true);

        float max = 0f;

        foreach (
            ParticleSystem ps
            in particles)
        {
            if (ps == null)
            {
                continue;
            }

            var main =
                ps.main;

            float duration =
                main.duration +
                main.startDelay
                    .constant;

            if (duration > max)
            {
                max = duration;
            }
        }

        return max;
    }
}
