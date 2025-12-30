// Shitmed compatibility extension.

using System.Collections.Generic;
using Robust.Shared.Serialization;

namespace Content.Shared.Damage;

public sealed partial class DamageSpecifier
{
    /// <summary>
    /// Shitmed: optional per-damage-type wound severity multipliers applied when creating wounds.
    /// </summary>
    [DataField]
    public Dictionary<string, float> WoundSeverityMultipliers { get; set; } = new();
}
