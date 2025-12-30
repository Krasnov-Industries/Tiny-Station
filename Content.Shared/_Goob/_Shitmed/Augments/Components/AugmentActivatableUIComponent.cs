// Goob Shitmed import: links an augment to a UI key.
using Robust.Shared.GameStates;

namespace Content.Shared._Goob._Shitmed.Augments.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class AugmentActivatableUIComponent : Component
{
    [DataField]
    public Enum? Key { get; set; }
}
