using Content.Server._Goobstation.SpaceWhale.Brain;
using Content.Server._Goobstation.SpaceWhale.Threat;
using Content.Shared.CCVar;
using Content.Shared.Damage.Systems;
using Content.Shared.Devour.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Item;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.PAI;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._Goobstation.SpaceWhale.Consume;

/// <summary>
/// Auto-eats nearby corpses (any MobState.Dead); digestion heals the whale later.
/// Eaten bodies get a WhaleEatenCorpseComponent so they can be cleaned up later.
/// </summary>
public sealed partial class WhaleConsumeSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private WhaleThreatSystem _threat = default!;
    [Dependency] private WhaleBrainSystem _brain = default!;

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

            // pAI and other living items are handled as items, not corpses.
            if (HasComp<ItemComponent>(target))
                continue;

            // Eat both dead and incapacitated (critical) — easy meal for the whale.
            if (!_mobState.IsIncapacitated(target, candidate.Comp))
                continue;

            _brain.RememberDeathScent(whale, Transform(target).Coordinates);

            // Tag before inserting so it survives a later gib release.
            var tag = EnsureComp<WhaleEatenCorpseComponent>(target);
            tag.EatenAt = _timing.CurTime;
            tag.EatenBy = whale;
            tag.PreserveInStomach = false;

            if (!_container.Insert(target, devourer.Stomach))
            {
                // Insert failed (anchored, too big, etc.) — drop the tag too.
                RemComp<WhaleEatenCorpseComponent>(target);
                continue;
            }

            ate++;
        }

        foreach (var candidate in _lookup.GetEntitiesInRange<PAIComponent>(xform.Coordinates, consumer.SearchRadius))
        {
            var target = candidate.Owner;
            if (target == whale || HasComp<WhaleEatenCorpseComponent>(target))
                continue;

            var tag = EnsureComp<WhaleEatenCorpseComponent>(target);
            tag.EatenAt = _timing.CurTime;
            tag.EatenBy = whale;
            tag.PreserveInStomach = true;

            if (!_container.Insert(target, devourer.Stomach))
            {
                RemComp<WhaleEatenCorpseComponent>(target);
                continue;
            }

            ate++;
        }

        foreach (var candidate in _lookup.GetEntitiesInRange<ItemComponent>(xform.Coordinates, consumer.SearchRadius))
        {
            var target = candidate.Owner;
            if (target == whale ||
                HasComp<PAIComponent>(target) ||
                HasComp<WhaleEatenCorpseComponent>(target) ||
                HasComp<WhaleSpawnedByComponent>(target) ||
                HasComp<SpaceWhaleSegmentComponent>(target) ||
                _container.IsEntityInContainer(target))
            {
                continue;
            }

            if (!_container.Insert(target, devourer.Stomach))
                continue;

            QueueDel(target);
            ate++;
        }

        if (ate > 0)
            _threat.LogWhale($"Consumed {ate} target(s)");
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
    [Dependency] private DamageableSystem _damageable = default!;

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
            if (eaten.PreserveInStomach)
                continue;

            if (now - eaten.EatenAt < maxAge)
                continue;

            if (eaten.EatenBy is { } whale && !TerminatingOrDeleted(whale))
            {
                var heal = FixedPoint2.New(-_cfg.GetCVar(CCVars.WhaleConsumeHeal));
                _damageable.HealDistributed(whale, heal, origin: whale);
            }

            QueueDel(uid);
        }
    }
}
