using Content.Server._Goobstation.SpaceWhale.Threat;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Item;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Goobstation.SpaceWhale.AI;

/// <summary>
/// Топорная память: только TopAggressor — кто за последние N секунд нанёс больше всех урона киту.
/// </summary>
public sealed partial class WhaleMemorySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private WhaleThreatSystem _threat = default!;

    private readonly List<EntityUid> _attackersToRemove = new();

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WhaleMemoryComponent, DamageDealtEvent>(OnDamageDealt);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(5);
        var query = EntityQueryEnumerator<WhaleMemoryComponent>();
        while (query.MoveNext(out var uid, out var comp))
            UpdateTopAggressor(uid, comp);
    }

    private void OnDamageDealt(Entity<WhaleMemoryComponent> ent, ref DamageDealtEvent args)
    {
        if (args.Origin == null || args.Damage.GetTotal() <= 0)
            return;

        if (!TryResolveDamageOrigin(ent.Owner, args.Origin.Value, out var origin))
            return;

        var history = ent.Comp.DamageHistory.GetOrNew(origin);
        history.Add(new WhaleDamageRecord { Time = _timing.CurTime, Amount = args.Damage.GetTotal() });

        // Goobstation edit start - force the leviathan to chase whoever damaged any body segment
        ent.Comp.TopAggressor = origin;
        if (TryComp<WhaleBrainComponent>(ent.Owner, out var brain))
        {
            brain.ForcedHuntTarget = origin;
            brain.ForcedHuntFromDamage = true;
            brain.NextForcedHuntAt = _timing.CurTime;
            brain.CurrentTarget = origin;
        }
        // Goobstation edit end
    }

    // Goobstation edit start - hitscan damage reports the gun, not the shooter
    private bool TryResolveDamageOrigin(EntityUid whale, EntityUid rawOrigin, out EntityUid origin)
    {
        origin = rawOrigin;
        if (Deleted(origin))
            return false;

        for (var i = 0; i < 8 && HasComp<ItemComponent>(origin); i++)
        {
            var xform = Transform(origin);
            if (!xform.ParentUid.IsValid() ||
                xform.ParentUid == origin ||
                Deleted(xform.ParentUid))
            {
                break;
            }

            origin = xform.ParentUid;
        }

        if (origin == whale ||
            Deleted(origin) ||
            HasComp<ItemComponent>(origin) ||
            HasComp<WhaleSpawnedByComponent>(origin) ||
            HasComp<SpaceWhaleSegmentComponent>(origin))
        {
            return false;
        }

        return true;
    }
    // Goobstation edit end

    private void UpdateTopAggressor(EntityUid uid, WhaleMemoryComponent comp)
    {
        var cutoff = _timing.CurTime - TimeSpan.FromSeconds(comp.AggressionWindow);
        EntityUid? top = null;
        var topDamage = FixedPoint2.Zero;

        _attackersToRemove.Clear();
        foreach (var (attacker, records) in comp.DamageHistory)
        {
            records.RemoveAll(record => record.Time < cutoff);
            if (records.Count == 0 || Deleted(attacker))
            {
                _attackersToRemove.Add(attacker);
                continue;
            }

            var total = FixedPoint2.Zero;
            foreach (var record in records)
                total += record.Amount;

            if (total <= topDamage)
                continue;

            top = attacker;
            topDamage = total;
        }

        if (top != comp.TopAggressor)
        {
            comp.TopAggressor = top;
            if (top != null)
                _threat.LogWhale($"Top aggressor: {ToPrettyString(top.Value)} ({topDamage} dmg in {comp.AggressionWindow:0}s)");
        }

        foreach (var attacker in _attackersToRemove)
            comp.DamageHistory.Remove(attacker);
    }
}
