using Content.Shared._SpaceDream.SpeechBarks.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._SpaceDream.SpeechBarks.Components;

[RegisterComponent]
public sealed partial class SpeechBarksComponent : Component
{
    [DataField]
    public ProtoId<BarkPrototype>? BarkPrototype;

    [DataField]
    public bool RandomlyAssigned;
}
