// Goob Shitmed import: attaches an action prototype to an augment.
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Goob._Shitmed.Augments.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class AugmentActionComponent : Component
{
    [DataField]
    public EntProtoId? Action { get; set; }
}
