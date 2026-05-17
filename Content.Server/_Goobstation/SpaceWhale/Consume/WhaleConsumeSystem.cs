using Content.Server._Goobstation.SpaceWhale.Threat;
using Content.Shared.CCVar;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Devour.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._Goobstation.SpaceWhale.Consume;

/// <summary>
/// Auto-eats nearby corpses (any MobState.Dead) and heals the whale.
/// Eaten bodies get a WhaleEatenCorpseComponent so they can be cleaned up later.
/// </summary>
public sealed partial class WhaleConsumeSystem : EntitySystem
{
    private static readonly string[] HealedDamageTypes =
    [
        "Blunt",
        "Slash",
        "Piercing",
        "Heat",
        "Cold",
        "Shock",
        "Caustic",
        "Poison",
        "Radiation",
        "Asphyxiation",
        "Bloodloss",
    ];

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private WhaleThreatSystem _threat = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<WhaleConsumerComponent, DevourerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var consumer, out var devourer, out var xform))
        {
            if (now < consumer.NextScan)
                continue;

            consumer.NextScan = now + TimeSpan.FromSeconds(consumer.ScanInterval);
            TryConsume(uid, consumer, devourer, xform);
        }
    }

    private void TryConsume(EntityUid whale, WhaleConsumerComponent consumer, DevourerComponent devourer, TransformComponent xform)
    {
        var ate = 0;
        DamageSpecifier? healSpec = null;

        foreach (var candidate in _lookup.GetEntitiesInRange<MobStateComponent>(xform.Coordinates, consumer.SearchRadius))
        {
            var target = candidate.Owner;
            if (target == whale)
                continue;

            // Don't eat other whales/segments.
            if (HasComp<WhaleSpawnedByComponent>(target))
                continue;

            // Already eaten by someone? Skip.
            if (HasComp<WhaleEatenCorpseComponent>(target))
                continue;

            // Eat both dead and incapacitated (critical) — easy meal for the whale.
            if (!_mobState.IsIncapacitated(target, candidate.Comp))
                continue;

            // Tag before inserting so it survives a later gib release.
            var tag = EnsureComp<WhaleEatenCorpseComponent>(target);
            tag.EatenAt = _timing.CurTime;

            if (!_container.Insert(target, devourer.Stomach))
            {
                // Insert failed (anchored, too big, etc.) — drop the tag too.
                RemComp<WhaleEatenCorpseComponent>(target);
                continue;
            }

            // Универсальное лечение — снимаем `heal` единиц с каждого
            // основного типа урона. Если у кита нет урона этого типа, ничего
            // не происходит (TryChangeDamage не уходит ниже 0).
            var heal = healSpec ??= CreateHealSpecifier(_cfg.GetCVar(CCVars.WhaleConsumeHeal));
            _damageable.TryChangeDamage(whale, heal, true, origin: whale);

            ate++;
        }

        if (ate > 0)
            _threat.LogWhale($"Consumed {ate} corpse(s), healed");
    }

    private static DamageSpecifier CreateHealSpecifier(float heal)
    {
        var spec = new DamageSpecifier();
        var amount = FixedPoint2.New(-heal);

        foreach (var type in HealedDamageTypes)
            spec.DamageDict[type] = amount;

        return spec;
    }
}

/// <summary>
/// Deletes eaten corpses N seconds after they were swallowed,
/// no matter whether they're still in the stomach or got released on gib.
/// </summary>
public sealed partial class WhaleStomachCleanupSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;

    private TimeSpan _nextTick;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextTick)
            return;

        _nextTick = now + TimeSpan.FromSeconds(30);

        var maxAge = TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.WhaleStomachCleanupSeconds));
        var query = EntityQueryEnumerator<WhaleEatenCorpseComponent>();
        while (query.MoveNext(out var uid, out var eaten))
        {
            if (now - eaten.EatenAt < maxAge)
                continue;

            QueueDel(uid);
        }
    }
}
