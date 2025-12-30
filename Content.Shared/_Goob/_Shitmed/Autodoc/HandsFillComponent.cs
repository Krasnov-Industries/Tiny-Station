// SPDX-FileCopyrightText: 2025 JohnOakman <sremy2012@hotmail.fr>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Shared._Goob._Shitmed.Autodoc;

/// <summary>
/// Specifies which hand slots on an entity should be pre-populated on spawn.
/// </summary>
[RegisterComponent, ComponentProtoName("HandsFill")]
public sealed partial class HandsFillComponent : Component
{
    /// <summary>
    /// Hand ID -> prototype to spawn and place. Null means just ensure the hand exists.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<string, EntProtoId?> Hands = new();
}
