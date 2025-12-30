// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Goob._Shitmed.Autodoc;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Map;

namespace Content.Server._Goob._Shitmed.Autodoc;

/// <summary>
/// Server-side filler that ensures specified hands exist and optionally spawns items into them.
/// </summary>
public sealed class HandsFillSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<HandsFillComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<HandsFillComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp(ent, out HandsComponent? handsComp))
            return;

        var handsEntity = (ent.Owner, handsComp);
        var coords = Transform(ent).Coordinates;
        foreach (var (handId, proto) in ent.Comp.Hands)
        {
            if (!_hands.TryGetHand(handsEntity, handId, out _))
                _hands.AddHand(handsEntity, handId, HandLocation.Middle);

            if (proto is null)
                continue;

            var item = Spawn(proto, coords);
            _hands.TryPickup(ent.Owner, item, handId, checkActionBlocker: false, animateUser: false, animate: false, handsComp: handsComp);
        }

        RemCompDeferred<HandsFillComponent>(ent);
    }
}
