// Shitmed compatibility extension.

using System;
using Robust.Shared.Serialization;

namespace Content.Shared.Damage.Components;

public sealed partial class DamageableComponent
{
    /// <summary>
    /// Shitmed: last time this damageable was modified, used by healing timers.
    /// </summary>
    [DataField]
    public TimeSpan LastModifiedTime = TimeSpan.Zero;
}
