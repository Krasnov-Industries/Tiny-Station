using Robust.Shared.Audio;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._SpaceDream.SpeechBarks.Prototypes;

[Prototype("speechBark")]
public sealed partial class BarkPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public bool RoundStart = true;

    [DataField]
    public LocId Name = "bark-human1-name";

    [DataField]
    public string Category = "standard";

    [DataField(required: true)]
    public SoundSpecifier Sound = default!;

    [DataField]
    public float Pitch = 1f;

    [DataField]
    public float PitchJitter = 0.04f;

    [DataField]
    public float MinDelay = 0.045f;

    [DataField]
    public float MaxDelay = 0.095f;

    [DataField]
    public int CharactersPerBark = 2;

    [DataField]
    public int MaxBarks = 90;
}

[Serializable, NetSerializable]
public readonly record struct BarkPlaybackData(
    ProtoId<BarkPrototype> Proto,
    SoundSpecifier Sound,
    float Pitch,
    float PitchJitter,
    float MinDelay,
    float MaxDelay,
    int Count);
