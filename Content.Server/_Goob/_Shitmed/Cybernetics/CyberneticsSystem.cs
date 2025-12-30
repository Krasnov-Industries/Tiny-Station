// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 SX-7 <sn1.test.preria.2002@gmail.com>
// SPDX-FileCopyrightText: 2025 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 gluesniffler <linebarrelerenthusiast@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Goob._Shitmed.Body.Events;
using Content.Shared._Goob._Shitmed.Body.Organ;
using Content.Shared._Goob._Shitmed.Cybernetics;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Damage.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Robust.Shared.Prototypes;

namespace Content.Server._Goob._Shitmed.Cybernetics;

internal sealed class CyberneticsSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CyberneticsComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<CyberneticsComponent, EmpDisabledRemovedEvent>(OnEmpDisabledRemoved);
    }

    private void OnEmpPulse(Entity<CyberneticsComponent> cyberEnt, ref EmpPulseEvent ev)
    {
        if (cyberEnt.Comp.Disabled)
            return;

        ev.Affected = true;
        ev.Disabled = true;
        cyberEnt.Comp.Disabled = true;

        if (HasComp<OrganComponent>(cyberEnt))
        {
            var disableEvent = new OrganEnableChangedEvent(false);
            RaiseLocalEvent(cyberEnt, ref disableEvent);
            return;
        }

        if (!TryComp(cyberEnt, out BodyPartComponent? part))
            return;

        var partDisable = new BodyPartEnableChangedEvent(false);
        RaiseLocalEvent(cyberEnt, ref partDisable);

        if (!TryComp(cyberEnt, out DamageableComponent? damageable))
            return;

        var shock = new DamageSpecifier(_prototypes.Index<DamageTypePrototype>("Shock"), 30);
        _damageable.TryChangeDamage((cyberEnt.Owner, damageable), shock, ignoreResistances: true);
        Dirty(cyberEnt, damageable);
    }

    private void OnEmpDisabledRemoved(Entity<CyberneticsComponent> cyberEnt, ref EmpDisabledRemovedEvent ev)
    {
        if (!cyberEnt.Comp.Disabled)
            return;

        cyberEnt.Comp.Disabled = false;

        if (HasComp<OrganComponent>(cyberEnt))
        {
            var enableEvent = new OrganEnableChangedEvent(true);
            RaiseLocalEvent(cyberEnt, ref enableEvent);
            return;
        }

        if (!HasComp<BodyPartComponent>(cyberEnt))
            return;

        var enablePart = new BodyPartEnableChangedEvent(true);
        RaiseLocalEvent(cyberEnt, ref enablePart);
    }
}
