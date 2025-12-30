// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._Goob._Shitmed.Clothing.Components;

[RegisterComponent]
public sealed partial class ClothingGrantTagComponent : Component
{
    [DataField("tag", required: true), ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<TagPrototype> Tag = default!;

    [ViewVariables(VVAccess.ReadWrite)]
    public bool IsActive;
}
