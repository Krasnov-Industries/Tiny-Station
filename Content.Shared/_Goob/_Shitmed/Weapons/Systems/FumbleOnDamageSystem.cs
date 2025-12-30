using Content.Shared.Body.Systems;
using Content.Shared.Body.Part;
using Content.Shared.Hands.Components;
using Content.Shared._Goob._Shitmed.Weapons.Melee.Events;
using Content.Shared._Goob._Shitmed.Weapons.Ranged.Events;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Melee;
using Content.Shared.Wieldable.Components;

namespace Content.Shared._Goob._Shitmed.Weapons.Systems;

public sealed class FumbleOnDamageSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MeleeWeaponComponent, AttemptMeleeEvent>(OnAttemptMeleeEvent);
        SubscribeLocalEvent<HandsComponent, GunShotBodyEvent>(OnAttemptShootEvent);
    }

    private void OnAttemptMeleeEvent(Entity<MeleeWeaponComponent> weapon, ref AttemptMeleeEvent ev)
    {
        if (ev.Cancelled || ev.User == EntityUid.Invalid || !TryComp(ev.User, out HandsComponent? hands))
            return;

        bool raiseOnAll = weapon.Comp.MustBeEquippedToUse
                          || TryComp(weapon.Owner, out WieldableComponent? wieldable)
                          && wieldable.Wielded;
        // This might get messy with furry species that have more than two hands, but who cares.

        var hand = _hands.GetActiveHand((ev.User, hands));
        var ev2 = new AttemptHandsMeleeEvent();
        if (raiseOnAll)
        {
            RaiseLocalEvent(ev.User, ev2);
        }
        else if (hand != null) // I dont think its possible for it to be null???
        {
            foreach (var part in _body.GetBodyChildrenOfType(ev.User, BodyPartType.Hand))
            {
                // Holy shit I need to add slotId assignment to each part this is so ass :wilted_rose:
                if (SharedBodySystem.GetPartSlotContainerId(part.Component.ParentSlot?.Id ?? "") == hand)
                {
                    ev2 = new AttemptHandsMeleeEvent(part.Component.Symmetry);
                    RaiseLocalEvent(part.Id, ev2);
                }
            }
        }

        if (ev2.Cancelled)
        {
            ev.Cancelled = true;
            return;
        }
    }

    private void OnAttemptShootEvent(EntityUid uid, HandsComponent hands, GunShotBodyEvent ev)
    {
        if (ev.GunUid == uid) // If the gun is the same user with a component e.g. laser eyes, dont bother.
            return;

        bool raiseOnAll = TryComp(ev.GunUid, out WieldableComponent? wieldable)
                          && wieldable.Wielded;

        var hand = _hands.GetActiveHand((uid, hands));
        var ev2 = new AttemptHandsShootEvent();
        if (raiseOnAll)
        {
            RaiseLocalEvent(uid, ev2);
        }
        else if (hand != null)
        {
            foreach (var part in _body.GetBodyChildrenOfType(uid, BodyPartType.Hand))
            {
                if (SharedBodySystem.GetPartSlotContainerId(part.Component.ParentSlot?.Id ?? "") == hand)
                {
                    ev2 = new AttemptHandsShootEvent(part.Component.Symmetry);
                    RaiseLocalEvent(part.Id, ev2);
                }
            }
        }
    }
}
