using System;
using UnityEngine;

[Serializable]
public class VoicePreviewData : BasePreviewData
{
    public AudioClip Voice;
    public float LocalTime;

    public VoicePreviewData(
        AudioClip voice,
        float localTime)
    {
        Voice = voice;
        LocalTime = localTime;
    }
}