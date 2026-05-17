using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Goobstation.SpaceWhale.Damage;

/// <summary>
/// Урон при ПОСТОЯННОМ контакте, а не разово на StartCollideEvent.
/// Каждый тик проходим по активным контактам entity и наносим урон каждой
/// цели, у которой прошёл per-target cooldown. Это даёт ощущение "кит
/// вгрызается" — пока туша касается стены, она крошится; не двигается ровно
/// — не теряет уроном.
///
/// Фильтры:
///   * не бить себя и своих сегментов / собратьев-китов;
///   * не уничтожать предметы (ItemComponent — рюкзаки, оружие, мелочёвка);
///   * RequirePushing — для сегментов: бить только когда сегмент реально
///     упирается (распрямлён сильнее spacing × PushDistanceFactor).
/// </summary>
public sealed partial class DamageOnCollideSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;

    private readonly HashSet<EntityUid> _contactsBuffer = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DamageOnCollideComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var dmg, out var phys))
        {
            _contactsBuffer.Clear();
            _physics.GetContactingEntities(uid, _contactsBuffer);

            foreach (var other in _contactsBuffer)
                TryDamage(uid, dmg, other, now);

            TryDamageNearby(uid, dmg, now);
        }
    }

    private void TryDamage(EntityUid owner, DamageOnCollideComponent dmg, EntityUid other, TimeSpan now)
    {
        var target = dmg.Inverted ? other : owner;
        TryDamageTarget(owner, dmg, target, now, false);
    }

    private void TryDamageNearby(EntityUid owner, DamageOnCollideComponent dmg, TimeSpan now)
    {
        if (dmg.NearbyDamageRadius <= 0f)
            return;

        if (!CanDamageFromOwner(owner, dmg))
            return;

        foreach (var target in _lookup.GetEntitiesInRange<DamageableComponent>(Transform(owner).Coordinates, dmg.NearbyDamageRadius))
            TryDamageTarget(owner, dmg, target.Owner, now, true);
    }

    private void TryDamageTarget(EntityUid owner, DamageOnCollideComponent dmg, EntityUid target, TimeSpan now, bool fromNearby)
    {
        if (target == owner || Deleted(target))
            return;

        // Свои — не трогаем.
        if (HasComp<WhaleSpawnedByComponent>(target) || HasComp<SpaceWhaleSegmentComponent>(target))
            return;

        // Предметы (рюкзаки, оружие на полу) — оставляем целыми.
        if (HasComp<ItemComponent>(target))
            return;

        // Radius sweep нужен для wallmount/структур без контакта; мобов он не
        // задевает, чтобы не превращать тело в невидимую урон-ауру.
        if (fromNearby && HasComp<MobStateComponent>(target))
            return;

        if (!CanDamageFromOwner(owner, dmg))
            return;

        // Per-target cooldown — частота "вгрызания" в одну и ту же цель.
        if (dmg.Cooldown > 0f
            && dmg.NextHit.TryGetValue(target, out var until)
            && now < until)
            return;

        _damageable.TryChangeDamage(target, dmg.Damage, origin: owner);

        if (dmg.Cooldown > 0f)
            dmg.NextHit[target] = now + TimeSpan.FromSeconds(dmg.Cooldown);
    }

    private bool CanDamageFromOwner(EntityUid owner, DamageOnCollideComponent dmg)
    {
        // Для сегмента: только когда реально упирается.
        return !dmg.RequirePushing ||
               !TryComp<SpaceWhaleSegmentComponent>(owner, out var seg) ||
               seg.IsPushing;
    }

}
