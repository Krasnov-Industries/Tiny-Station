using Content.Shared.Damage.Systems;
using Content.Shared.Item;
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
            if (_contactsBuffer.Count == 0)
                continue;

            foreach (var other in _contactsBuffer)
                TryDamage(uid, dmg, other, now);
        }
    }

    private void TryDamage(EntityUid owner, DamageOnCollideComponent dmg, EntityUid other, TimeSpan now)
    {
        var target = dmg.Inverted ? other : owner;
        if (target == owner || Deleted(target))
            return;

        // Свои — не трогаем.
        if (HasComp<WhaleSpawnedByComponent>(target) || HasComp<SpaceWhaleSegmentComponent>(target))
            return;

        // Предметы (рюкзаки, оружие на полу) — оставляем целыми.
        if (HasComp<ItemComponent>(target))
            return;

        // Для сегмента: только когда реально упирается.
        if (dmg.RequirePushing
            && TryComp<SpaceWhaleSegmentComponent>(owner, out var seg)
            && !seg.IsPushing)
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

}
