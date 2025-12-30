// SPDX-FileCopyrightText: 2025 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Goob._Shitmed.Medical.Surgery;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Popups;

namespace Content.Shared._Goob._Shitmed.Medical.Surgery.Steps;

/// <summary>
/// Applies self-damage to the surgeon during certain steps (e.g., xeno surgery).
/// </summary>
public sealed class SurgeryDamageUserSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SurgeryDamageUserComponent, SurgeryStepEvent>(OnSurgeryStep);
    }

    private void OnSurgeryStep(Entity<SurgeryDamageUserComponent> ent, ref SurgeryStepEvent args)
    {
        _damage.TryChangeDamage(args.User, ent.Comp.Damage);
        if (ent.Comp.Popup is not { } popup)
            return;

        var msg = Loc.GetString(popup, ("target", args.Body), ("part", args.Part));
        _popup.PopupPredicted(msg, args.Body, args.User, PopupType.SmallCaution);
    }
}
