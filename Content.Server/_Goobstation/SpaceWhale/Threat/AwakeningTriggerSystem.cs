using Content.Shared.CCVar;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Goobstation.SpaceWhale.Threat;

public sealed partial class AwakeningTriggerSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private WhaleThreatSystem _threat = default!;
    [Dependency] private NoiseSourceSystem _noise = default!;

    private static readonly ProtoId<TagPrototype> HeavyShipWeaponTag = "HeavyShipWeapon";

    private TimeSpan _nextDistanceCheck;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpaceWhaleExplosionEvent>(OnExplosion);
        SubscribeLocalEvent<GunComponent, GunShotEvent>(OnGunShot);
        _nextDistanceCheck = _timing.CurTime + TimeSpan.FromSeconds(30);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_cfg.GetCVar(CCVars.WhaleEnabled) || _timing.CurTime < _nextDistanceCheck)
            return;

        _nextDistanceCheck = _timing.CurTime + TimeSpan.FromSeconds(30);
        CheckFarHumanoids();
    }

    private void OnExplosion(SpaceWhaleExplosionEvent ev)
    {
        if (!_cfg.GetCVar(CCVars.WhaleEnabled))
            return;

        if (ev.TotalIntensity <= _cfg.GetCVar(CCVars.WhaleAwakenNukeForce) || HasGridAt(ev.Epicenter))
            return;

        _threat.Awaken("major explosion in space");
    }

    private void OnGunShot(Entity<GunComponent> gun, ref GunShotEvent args)
    {
        if (!_cfg.GetCVar(CCVars.WhaleEnabled))
            return;

        var xform = Transform(gun.Owner);
        if (xform.GridUid != null)
            return;

        if (!_threat.State.IsAwakened && _tag.HasTag(gun.Owner, HeavyShipWeaponTag))
        {
            _threat.Awaken("ship weapon in space");
            return;
        }

        _noise.AddGunNoise(gun.Owner);
    }

    private void CheckFarHumanoids()
    {
        var state = _threat.State;
        if (state.IsAwakened)
            return;

        var awakenDistance = _cfg.GetCVar(CCVars.WhaleAwakenDistance);
        var awakenTime = TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.WhaleAwakenDistanceTime));
        var query = EntityQueryEnumerator<HumanoidProfileComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var mobState, out var xform))
        {
            if (_threat.IsLivingHumanoidFarFromStation(uid, xform, mobState, awakenDistance))
            {
                if (!state.FarFromStationSince.TryGetValue(uid, out var since))
                    state.FarFromStationSince[uid] = _timing.CurTime;
                else if (_timing.CurTime - since >= awakenTime)
                    _threat.Awaken("player far from station");
            }
            else
            {
                state.FarFromStationSince.Remove(uid);
            }
        }
    }

    private bool HasGridAt(MapCoordinates coords)
    {
        return _mapManager.TryFindGridAt(coords, out _, out _);
    }
}
