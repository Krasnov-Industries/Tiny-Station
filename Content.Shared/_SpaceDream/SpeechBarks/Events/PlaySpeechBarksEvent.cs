using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared._SpaceDream.SpeechBarks.Events;

[Serializable, NetSerializable]
public sealed class PlaySpeechBarksEvent : EntityEventArgs
{
    public NetEntity Source;
    public SoundSpecifier Sound;
    public SpeechBarkPlaybackSegment[] Segments;
    public bool IsWhisper;
    public float MaxDistance;

    public PlaySpeechBarksEvent(
        NetEntity source,
        SoundSpecifier sound,
        SpeechBarkPlaybackSegment[] segments,
        bool isWhisper,
        float maxDistance)
    {
        Source = source;
        Sound = sound;
        Segments = segments;
        IsWhisper = isWhisper;
        MaxDistance = maxDistance;
    }
}

[Serializable, NetSerializable]
public readonly record struct SpeechBarkPlaybackSegment(
    float Pitch,
    float PitchJitter,
    float MinDelay,
    float MaxDelay,
    float PitchRamp,
    int Count,
    float VolumeMultiplier,
    float PauseAfter,
    bool LowpassFilter,
    SpeechBarkPitchStepStyle PitchStepStyle);

[Serializable, NetSerializable]
public enum SpeechBarkPitchStepStyle : byte
{
    Neutral,
    Emphatic,
    Question,
    Tired,
    Unstable,
}
