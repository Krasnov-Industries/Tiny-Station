// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Shared._Goob._Shitmed.Clothing.Components;

/// <summary>
/// Grants components to the wearer while this clothing is equipped.
/// </summary>
[RegisterComponent]
public sealed partial class ClothingGrantComponentComponent : Component
{
    [DataField("component", required: true)]
    [AlwaysPushInheritance]
    public ComponentRegistry Components { get; private set; } = new();

    // Tracks which entries were actually added so we can remove only those.
    [ViewVariables(VVAccess.ReadWrite)]
    public Dictionary<string, bool> Active = new();
}
