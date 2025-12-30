// Goob Shitmed import: marker for strength-enhancing arm augments.
using Robust.Shared.GameStates;

namespace Content.Shared._Goob._Shitmed.Augments.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class AugmentStrengthComponent : Component
{
    [DataField]
    public float Bonus { get; set; } = 0f;
}
