using System.Linq;
using Content.Server._Goobstation.SpaceWhale.Threat;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
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

        var history = ent.Comp.DamageHistory.GetOrNew(args.Origin.Value);
        history.Add(new WhaleDamageRecord { Time = _timing.CurTime, Amount = args.Damage.GetTotal() });
    }

    private void UpdateTopAggressor(EntityUid uid, WhaleMemoryComponent comp)
    {
        var cutoff = _timing.CurTime - TimeSpan.FromSeconds(comp.AggressionWindow);
        EntityUid? top = null;
        var topDamage = FixedPoint2.Zero;

        foreach (var (attacker, records) in comp.DamageHistory.ToArray())
        {
            records.RemoveAll(record => record.Time < cutoff);
            if (records.Count == 0)
            {
                comp.DamageHistory.Remove(attacker);
                continue;
            }

            var total = records.Aggregate(FixedPoint2.Zero, (sum, record) => sum + record.Amount);
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
    }
}
