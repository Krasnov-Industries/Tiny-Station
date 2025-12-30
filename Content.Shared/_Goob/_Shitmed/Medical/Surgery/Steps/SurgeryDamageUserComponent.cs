// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared._Goob._Shitmed.Medical.Surgery.Steps;

/// <summary>
/// Deals damage to the surgeon when a step is done.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(SurgeryDamageUserSystem))]
[ComponentProtoName("SurgeryDamageUser")]
public sealed partial class SurgeryDamageUserComponent : Component
{
    [DataField(required: true)]
    public DamageSpecifier Damage = new();

    /// <summary>
    /// Popup shown to everyone, gets passed "target" and "part".
    /// </summary>
    [DataField]
    public LocId? Popup;
}
