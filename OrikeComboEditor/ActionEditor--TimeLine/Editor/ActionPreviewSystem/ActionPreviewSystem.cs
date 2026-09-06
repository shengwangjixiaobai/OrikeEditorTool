using UnityEngine;
using UnityEditor;


public class ActionPreviewSystem
{
    private GameObject _character;

    private ActionData _actionData;


    public ActionPreviewSystem(
        GameObject character,
        ActionData actionData)
    {
        _character =
            character;

        _actionData =
            actionData;
    }


    // =========================================================
    // Set Character
    // =========================================================

    public void SetCharacter(
        GameObject character)
    {
        _character =
            character;
    }


    // =========================================================
    // Set Action Data
    // =========================================================

    public void SetActionData(
        ActionData actionData)
    {
        _actionData =
            actionData;
    }


    // =========================================================
    // Preview
    // =========================================================

    /// <summary>
    /// 将 Character 直接设置到指定时间的状态
    /// </summary>
    public void Preview(
        float actionTime)
    {
        if (_character == null)
        {
            return;
        }

        if (_actionData == null)
        {
            return;
        }

        foreach (
            TrackData track
            in _actionData.Tracks)
        {
            PreviewTrack(
                track,
                actionTime);
        }
    }


    // =========================================================
    // Preview Track
    // =========================================================

    private void PreviewTrack(
        TrackData track,
        float actionTime)
    {
        if (track == null)
        {
            return;
        }

        if (track.Clips == null)
        {
            return;
        }

        foreach (
            BaseClipData clip
            in track.Clips)
        {
            if (clip == null)
            {
                continue;
            }

            BasePreviewData previewData =
                clip.GetPreviewDataAtTime(
                    actionTime);

            if (previewData == null)
            {
                continue;
            }

            ApplyPreviewData(
                previewData);
        }
    }


    // =========================================================
    // Apply Preview Data
    // =========================================================

    private void ApplyPreviewData(
        BasePreviewData previewData)
    {
        if (previewData == null)
        {
            return;
        }

        if (
            previewData
            is AnimationPreviewData
                animationPreviewData)
        {
            ApplyAnimationPreview(
                animationPreviewData);
        }
    }


    // =========================================================
    // Apply Animation Preview
    // =========================================================

    private void ApplyAnimationPreview(
        AnimationPreviewData previewData)
    {
        if (_character == null)
        {
            Debug.LogError("Preview Character is null");
            return;
        }

        if (previewData.Animation == null)
        {
            Debug.LogError("Preview Animation is null");
            return;
        }

        Debug.Log(
            $"Preview Animation: {previewData.Animation.name}, " +
            $"Time: {previewData.LocalTime}");

        previewData.Animation.SampleAnimation(
            _character,
            previewData.LocalTime);

        SceneView.RepaintAll();
    }
}