// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 deltanedas <@deltanedas:kde.org>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Goob._Shitmed.Autodoc.Slots;

namespace Content.Shared._Goob._Shitmed.Autodoc;

/// <summary>
/// Declares automation slots on machines (stub for Goob automation support).
/// </summary>
[RegisterComponent, ComponentProtoName("AutomationSlots")]
public sealed partial class AutomationSlotsComponent : Component
{
    [DataField(required: true)]
    public List<AutomationSlot> Slots = new();
}
