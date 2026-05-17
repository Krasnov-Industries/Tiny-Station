using Content.Server.Shuttles.Components;
using Content.Shared.CCVar;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Tag;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Goobstation.SpaceWhale.Threat;

public sealed partial class NoiseSourceSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private WhaleThreatSystem _threat = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private SharedMapSystem _map = default!;

    private static readonly ProtoId<TagPrototype> HeavyShipWeaponTag = "HeavyShipWeapon";

    private TimeSpan _nextTick;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpaceWhaleExplosionEvent>(OnExplosion);
        _nextTick = _timing.CurTime + TimeSpan.FromSeconds(1);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_cfg.GetCVar(CCVars.WhaleEnabled) || !_threat.State.IsAwakened || _timing.CurTime < _nextTick)
            return;

        _nextTick = _timing.CurTime + TimeSpan.FromSeconds(1);
        AddShuttleNoise();
        AddEvaNoise();
    }

    private void OnExplosion(SpaceWhaleExplosionEvent ev)
    {
        if (!_threat.State.IsAwakened)
            return;

        var threatAmount = ev.TotalIntensity switch
        {
            > 200f => 60f,
            > 50f => 30f,
            > 30f => 15f,
            > 10f => 5f,
            _ => 0f,
        };

        if (threatAmount <= 0f)
            return;

        var mapUid = _map.GetMapOrInvalid(ev.Epicenter.MapId);
        var coords = new EntityCoordinates(mapUid, ev.Epicenter.Position);

        _threat.AddThreat(threatAmount);
        // Use raw explosion intensity as noise intensity — louder events propagate further.
        _threat.AddNoise(coords, ev.TotalIntensity);
    }

    public void AddGunNoise(EntityUid gun)
    {
        if (!_cfg.GetCVar(CCVars.WhaleEnabled) || !_threat.State.IsAwakened)
            return;

        var xform = Transform(gun);
        if (xform.GridUid != null)
            return;

        var isHeavy = _tag.HasTag(gun, HeavyShipWeaponTag);
        var threatAmount = isHeavy ? 2f : 0.2f;
        var noiseIntensity = isHeavy ? 5f : 1f;

        _threat.AddThreat(threatAmount);
        _threat.AddNoise(xform.Coordinates, noiseIntensity);
    }

    private void AddShuttleNoise()
    {
        var query = EntityQueryEnumerator<ShuttleComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var physics, out var xform))
        {
            if (physics.LinearVelocity.LengthSquared() < 0.01f)
                continue;

            if (!_threat.TryGetNearestStation(xform.Coordinates, out _, out _, out var distance) || distance > 500f)
                continue;

            // Shuttles feed threat (escalation), but don't pollute the per-position noise list.
            _threat.AddThreat(0.3f);
        }
    }

    private void AddEvaNoise()
    {
        var query = EntityQueryEnumerator<HumanoidProfileComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var mobState, out var xform))
        {
            if (xform.GridUid != null || !_mobState.IsAlive(uid, mobState))
                continue;

            _threat.AddThreat(0.05f);
        }
    }
}
