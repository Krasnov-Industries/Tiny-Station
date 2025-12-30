// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 deltanedas <@deltanedas:kde.org>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DeviceLinking;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Goob._Shitmed.Autodoc.Slots;

/// <summary>
/// Minimal stub of the Goob automation slot types so prototypes load.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class AutomationSlot
{
    [DataField]
    public ProtoId<SinkPortPrototype>? Input;

    [DataField]
    public ProtoId<SourcePortPrototype>? Output;

    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public EntityWhitelist? Blacklist;
}

public sealed partial class AutomatedStorage : AutomationSlot;

public sealed partial class AutomatedHand : AutomationSlot
{
    [DataField(required: true)]
    public string HandName = string.Empty;
}
