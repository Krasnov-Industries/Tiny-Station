// Shitmed compatibility extension.

using System;
using Content.Shared._Goob._Shitmed.Body;
using Robust.Shared.Serialization;

namespace Content.Shared.Body.Components;

public sealed partial class BodyComponent
{
    /// <summary>
    /// Shitmed: body complexity toggle. Defaults to complex to keep healing logic enabled.
    /// </summary>
    [DataField]
    public BodyType BodyType = BodyType.Complex;

    /// <summary>
    /// Shitmed: next time when background healing can run.
    /// </summary>
    [DataField]
    public TimeSpan HealAt = TimeSpan.Zero;
}
