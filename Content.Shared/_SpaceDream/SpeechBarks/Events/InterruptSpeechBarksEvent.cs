using Robust.Shared.Serialization;

namespace Content.Shared._SpaceDream.SpeechBarks.Events;

[Serializable, NetSerializable]
public sealed class InterruptSpeechBarksEvent : EntityEventArgs
{
    public NetEntity Source;
    public SpeechBarkInterruptKind Kind;

    public InterruptSpeechBarksEvent(NetEntity source, SpeechBarkInterruptKind kind)
    {
        Source = source;
        Kind = kind;
    }
}

[Serializable, NetSerializable]
public enum SpeechBarkInterruptKind : byte
{
    Damage,
    Death,
}
