// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Goob._Shitmed.Cybernetics;

/// <summary>
/// Marker for cybernetic organs/parts that can be disabled by EMPs.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[ComponentProtoName("Cybernetics")]
public sealed partial class CyberneticsComponent : Component
{
    /// <summary>
    ///     True while this cybernetic is disabled (EMP, etc).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Disabled;
}
