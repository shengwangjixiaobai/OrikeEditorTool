using UnityEditor;
using UnityEngine;

public class ActionPreviewSystem
{
    private GameObject _character;

    private ActionData _actionData;

    private AudioClip _currentVoice;

    private int _currentVoiceSample = -1;

    private float _lastActionTime = -1f;

    private float _voiceStopTime = -1f;

    private bool _voicePlaying;


    // =========================================================
    // Constructor
    // =========================================================

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
        if (_actionData != actionData)
        {
            StopPreview();
        }

        _actionData =
            actionData;
    }


    // =========================================================
    // Preview
    //
    // frameRate:
    //     直接使用 TimeLineWindow 当前 FPS
    //
    // isPlaying:
    //     false = 单帧预览
    //     true  = 连续播放
    // =========================================================

    public void Preview(
        float actionTime,
        float frameRate,
        bool isPlaying)
    {
        if (frameRate <= 0f)
        {
            frameRate = 60f;
        }

        if (_actionData == null)
        {
            StopVoiceIfNeeded();

            return;
        }

        if (_actionData.Tracks == null)
        {
            StopVoiceIfNeeded();

            return;
        }

        bool hasVoice = false;

        foreach (
            TrackData track
            in _actionData.Tracks)
        {
            if (track == null)
            {
                continue;
            }

            PreviewTrack(
                track,
                actionTime,
                frameRate,
                isPlaying,
                ref hasVoice);
        }

        if (!hasVoice)
        {
            StopVoiceIfNeeded();
        }

        _lastActionTime =
            actionTime;
    }


    // =========================================================
    // Preview Track
    // =========================================================

    private void PreviewTrack(
        TrackData track,
        float actionTime,
        float frameRate,
        bool isPlaying,
        ref bool hasVoice)
    {
        if (track == null ||
            track.Clips == null)
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

            if (previewData
                is VoicePreviewData)
            {
                hasVoice = true;
            }

            ApplyPreviewData(
                previewData,
                frameRate,
                isPlaying);
        }
    }


    // =========================================================
    // Apply Preview Data
    // =========================================================

    private void ApplyPreviewData(
        BasePreviewData previewData,
        float frameRate,
        bool isPlaying)
    {
        if (previewData == null)
        {
            return;
        }

        if (previewData
            is AnimationPreviewData)
        {
            AnimationPreviewData animationData =
                previewData
                as AnimationPreviewData;

            ApplyAnimationPreview(
                animationData);

            return;
        }

        if (previewData
            is VoicePreviewData)
        {
            VoicePreviewData voiceData =
                previewData
                as VoicePreviewData;

            ApplyVoicePreview(
                voiceData,
                frameRate,
                isPlaying);

            return;
        }
    }


    // =========================================================
    // Animation Preview
    // =========================================================

    private void ApplyAnimationPreview(
        AnimationPreviewData previewData)
    {
        if (_character == null)
        {
            return;
        }

        if (previewData == null)
        {
            return;
        }

        if (previewData.Animation == null)
        {
            return;
        }

        previewData.Animation.SampleAnimation(
            _character,
            previewData.LocalTime);

        SceneView.RepaintAll();
    }


    // =========================================================
    // Voice Preview
    // =========================================================

    private void ApplyVoicePreview(
        VoicePreviewData previewData,
        float frameRate,
        bool isPlaying)
    {
        if (previewData == null ||
            previewData.Voice == null)
        {
            return;
        }

        AudioClip voice =
            previewData.Voice;

        float localTime =
            Mathf.Max(
                0f,
                previewData.LocalTime);

        int sample =
            Mathf.RoundToInt(
                localTime *
                voice.frequency);

        sample =
            Mathf.Clamp(
                sample,
                0,
                Mathf.Max(
                    0,
                    voice.samples - 1));


        // =====================================================
        // 正常播放
        //
        // 同一个 Voice 不重复启动。
        // AudioUtil 会继续播放。
        // =====================================================

        if (isPlaying)
        {
            if (_currentVoice == voice)
            {
                return;
            }

            StopVoiceIfNeeded();

            _currentVoice =
                voice;

            _currentVoiceSample =
                sample;

            _voicePlaying =
                true;

            _voiceStopTime =
                -1f;

            EditorAudioPreview.Play(
                voice,
                sample);

            return;
        }


        // =====================================================
        // 非播放状态
        //
        // 当前时间轴停在哪一帧，
        // 就只预览这一帧。
        // =====================================================

        if (_currentVoice == voice &&
            _currentVoiceSample == sample &&
            _voicePlaying)
        {
            return;
        }

        StopVoiceIfNeeded();

        _currentVoice =
            voice;

        _currentVoiceSample =
            sample;


        float frameDuration =
            1f /
            Mathf.Max(
                1f,
                frameRate);


        _voiceStopTime =
            (float)EditorApplication.timeSinceStartup +
            frameDuration;

        _voicePlaying =
            true;


        EditorAudioPreview.Play(
            voice,
            sample);
    }


    // =========================================================
    // Update
    // =========================================================

    public void Update()
    {
        if (!_voicePlaying)
        {
            return;
        }

        if (_voiceStopTime < 0f)
        {
            return;
        }

        double currentTime =
            EditorApplication.timeSinceStartup;

        if (currentTime >= _voiceStopTime)
        {
            StopVoiceIfNeeded();
        }
    }


    // =========================================================
    // Stop Voice
    // =========================================================

    private void StopVoiceIfNeeded()
    {
        if (_currentVoice == null &&
            !_voicePlaying)
        {
            return;
        }

        EditorAudioPreview.Stop();

        _currentVoice =
            null;

        _currentVoiceSample =
            -1;

        _voiceStopTime =
            -1f;

        _voicePlaying =
            false;
    }


    // =========================================================
    // Stop Preview
    // =========================================================

    public void StopPreview()
    {
        EditorAudioPreview.Stop();

        _currentVoice =
            null;

        _currentVoiceSample =
            -1;

        _lastActionTime =
            -1f;

        _voiceStopTime =
            -1f;

        _voicePlaying =
            false;
    }
}