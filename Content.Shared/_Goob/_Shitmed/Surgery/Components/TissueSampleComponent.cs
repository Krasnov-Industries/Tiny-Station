// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Goob._Shitmed.Surgery.Components;

/// <summary>
/// Marker placed on extracted tissue samples so surgery steps can require the item.
/// </summary>
[RegisterComponent]
[NetworkedComponent]
public sealed partial class TissueSampleComponent : Component;

/// <summary>
/// Marker placed on a body/organ that already has a tissue sample grafted.
/// Only used by the Goob xeno surgery steps.
/// </summary>
[RegisterComponent]
[NetworkedComponent]
public sealed partial class HasTissueSampleComponent : Component;
