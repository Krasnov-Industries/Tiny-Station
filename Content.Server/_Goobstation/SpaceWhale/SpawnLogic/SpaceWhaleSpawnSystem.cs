using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Server._Goobstation.SpaceWhale.Brain;
using Content.Server._Goobstation.SpaceWhale.Threat;
using Content.Shared.CCVar;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._Goobstation.SpaceWhale.SpawnLogic;

public sealed partial class SpaceWhaleSpawnSystem : EntitySystem
{
    private const float SpawnDistanceFromStation = 2000f;

    private static readonly SoundSpecifier SpawnSound = new SoundPathSpecifier("/Audio/_Goobstation/Ambience/SpaceWhale/leviathan-appear.ogg");

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private WhaleThreatSystem _threat = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private WhaleBrainSystem _brain = default!;

    public bool TrySpawn(EntityCoordinates? preferred = null, bool force = false)
    {
        if (!_cfg.GetCVar(CCVars.WhaleEnabled))
            return false;

        var state = _threat.State;
        if (state.CurrentWhale != null && !Deleted(state.CurrentWhale.Value))
            return false;

        if (!force && preferred == null && state.Threat < _cfg.GetCVar(CCVars.WhaleThreatSpawnAt))
            return false;

        EntityCoordinates? origin = preferred;
        if (origin == null && _threat.TryGetSpawnOrigin(out var spawnOrigin))
            origin = spawnOrigin;

        EntityCoordinates? station = null;
        if (origin is { } knownOrigin &&
            _threat.TryGetNearestStation(knownOrigin, out _, out var nearestStation, out _))
            station = nearestStation;
        else if (_threat.TryGetRandomStationPoint(out var stationCoords))
            station = stationCoords;

        if (preferred == null && origin == null && station == null)
            return false;

        MapCoordinates spawnMap;
        if (preferred != null)
        {
            spawnMap = _transform.ToMapCoordinates(preferred.Value);
        }
        else
        {
            var anchor = station ?? origin!.Value;
            var mapOrigin = _transform.ToMapCoordinates(anchor);
            var angle = _random.NextAngle();
            spawnMap = new MapCoordinates(mapOrigin.Position + angle.ToVec() * SpawnDistanceFromStation, mapOrigin.MapId);
        }

        if (spawnMap.MapId == MapId.Nullspace)
            return false;

        var mapUid = _map.GetMapOrInvalid(spawnMap.MapId);
        var spawnCoords = new EntityCoordinates(mapUid, spawnMap.Position);

        var whale = Spawn("SpaceLeviathan", spawnCoords);
        _transform.AttachToGridOrMap(whale);
        if (TryComp<PhysicsComponent>(whale, out var physics))
            _physics.SetLinearVelocity(whale, Vector2.Zero, body: physics);

        _threat.SetCurrentWhale(whale);
        _brain.RememberActivity(whale, spawnCoords);
        _audio.PlayGlobal(SpawnSound, Filter.Broadcast(), true);
        _threat.PlayWhalePresenceCue(whale);
        _threat.LogWhale($"Spawned at {spawnMap}");
        _chat.DispatchGlobalAnnouncement(Loc.GetString("threat-approaching-announcement"), colorOverride: Color.Gold);
        return true;
    }
}
