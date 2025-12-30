// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Wires;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Content.Server._Goob._Shitmed.Autodoc;

/// <summary>
/// Minimal stub to satisfy the Goob autodoc wire layout.
/// </summary>
public sealed partial class AutodocSafetyWireAction : BaseToggleWireAction
{
    [DataField("name")]
    public override string Name { get; set; } = "wire-name-autodoc-safety";

    [DataField("color")]
    public override Color Color { get; set; } = Color.LimeGreen;

    public override object? StatusKey => "AutodocSafety";

    public override void ToggleValue(EntityUid owner, bool setting)
    {
        // Stub: no behavior needed for prototype validation.
    }

    public override bool GetValue(EntityUid owner) => true;
}
