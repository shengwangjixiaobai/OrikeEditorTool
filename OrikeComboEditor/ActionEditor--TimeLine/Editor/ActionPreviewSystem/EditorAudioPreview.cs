using System;
using System.Reflection;
using UnityEngine;

public static class EditorAudioPreview
{
    private static MethodInfo _playClipMethod;

    private static MethodInfo _stopAllClipsMethod;

    private static bool _initialized;

    private static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        Type audioUtilType =
            Type.GetType(
                "UnityEditor.AudioUtil, UnityEditor");

        if (audioUtilType == null)
        {
            return;
        }

        _playClipMethod =
            audioUtilType.GetMethod(
                "PlayPreviewClip",
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic,
                null,
                new Type[]
                {
                    typeof(AudioClip),
                    typeof(int),
                    typeof(bool)
                },
                null);

        _stopAllClipsMethod =
            audioUtilType.GetMethod(
                "StopAllPreviewClips",
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic);
    }

    public static void Play(
        AudioClip clip,
        int startSample = 0)
    {
        if (clip == null)
        {
            return;
        }

        Initialize();

        if (_playClipMethod == null)
        {
            return;
        }

        try
        {
            _playClipMethod.Invoke(
                null,
                new object[]
                {
                    clip,
                    startSample,
                    false
                });
        }
        catch
        {
        }
    }

    public static void Stop()
    {
        Initialize();

        if (_stopAllClipsMethod == null)
        {
            return;
        }

        try
        {
            _stopAllClipsMethod.Invoke(
                null,
                null);
        }
        catch
        {
        }
    }
}