using System;
using UnityEngine;

/// <summary>
/// 挂载并设置本地变换点事件
///
/// 触发时将指定物体重新挂载（SetParent）到目标父物体下，
/// 并设置其相对父物体的完整本地变换（相当于 Inspector 里的 Reset）：
///   - 目标物体名：要被挂载的物体（如刀）；
///   - 父物体名：挂载到哪个物体下（如另一只手的骨骼/武器挂点）；
///   - 本地位置：localPosition，默认 (0, 0, 0)；
///   - 本地旋转：localEulerAngles（欧拉角），默认 (0, 0, 0)；
///   - 本地缩放：localScale，默认 (1, 1, 1)。
///
/// 物体名从角色根节点递归查找（与特效挂点 DeepFind 一致），
/// 因此可以引用场景中的刀、手部骨骼等物体。
///
/// 典型用法：拔刀动作播放到结尾时，把刀从一只手挂到另一只手的挂点。
/// </summary>
[Serializable]
public class SetPositionPointEvent
    : IPointEvent
{
    [SerializeField]
    private string _targetName;

    [SerializeField]
    private string _parentName;

    [SerializeField]
    private Vector3 _localPosition =
        Vector3.zero;

    [SerializeField]
    private Vector3 _localEulerAngles =
        Vector3.zero;

    [SerializeField]
    private Vector3 _localScale =
        Vector3.one;


    public void OnCall(
        GameObject owner)
    {
        if (owner == null)
        {
            Debug.LogWarning(
                "[SetPositionPointEvent] owner 为空，无法挂载物体。");

            return;
        }

        if (string.IsNullOrEmpty(
                _targetName))
        {
            Debug.LogWarning(
                "[SetPositionPointEvent] 未配置目标物体名，已跳过。");

            return;
        }

        if (string.IsNullOrEmpty(
                _parentName))
        {
            Debug.LogWarning(
                "[SetPositionPointEvent] 未配置父物体名，已跳过。");

            return;
        }

        Transform target =
            FindByName(
                owner.transform,
                _targetName);

        if (target == null)
        {
            Debug.LogWarning(
                $"[SetPositionPointEvent] 找不到目标物体 \"{_targetName}\"，已跳过。");

            return;
        }

        Transform parent =
            FindByName(
                owner.transform,
                _parentName);

        if (parent == null)
        {
            Debug.LogWarning(
                $"[SetPositionPointEvent] 找不到父物体 \"{_parentName}\"，已跳过。");

            return;
        }

        // 挂载到父物体下。worldPositionStays 传 false：
        // 不保留目标原来的世界姿态，随后直接写入完整本地变换，
        // 效果等同于挂载后在 Inspector 执行 Reset。
        target.SetParent(
            parent,
            false);

        target.localPosition =
            _localPosition;

        target.localRotation =
            Quaternion.Euler(
                _localEulerAngles);

        target.localScale =
            _localScale;
    }


    /// <summary>
    /// 按名称递归查找子 Transform。
    /// </summary>
    private static Transform FindByName(
        Transform root,
        string name)
    {
        if (root == null ||
            string.IsNullOrEmpty(name))
        {
            return null;
        }

        if (root.name == name)
        {
            return root;
        }

        foreach (
            Transform child
            in root)
        {
            Transform found =
                FindByName(
                    child,
                    name);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
