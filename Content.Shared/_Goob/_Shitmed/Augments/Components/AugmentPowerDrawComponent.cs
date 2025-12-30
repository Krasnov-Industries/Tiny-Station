// Goob Shitmed import: tracks passive/active power draw for augments.
using Robust.Shared.GameStates;

namespace Content.Shared._Goob._Shitmed.Augments.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class AugmentPowerDrawComponent : Component
{
    [DataField]
    public float Draw { get; set; } = 0f;
}
