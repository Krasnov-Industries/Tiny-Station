using System.Numerics;
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
    private static readonly SoundSpecifier SpawnSound = new SoundPathSpecifier("/Audio/_Goobstation/Ambience/SpaceWhale/leviathan-appear.ogg");

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private WhaleThreatSystem _threat = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;

    public bool TrySpawn(EntityCoordinates? preferred = null)
    {
        if (!_cfg.GetCVar(CCVars.WhaleEnabled))
            return false;

        var state = _threat.State;
        if (state.CurrentWhale != null && !Deleted(state.CurrentWhale.Value))
            return false;

        if (preferred == null && state.Threat < _cfg.GetCVar(CCVars.WhaleThreatSpawnAt))
            return false;

        EntityCoordinates? origin = preferred;
        if (origin == null && _threat.TryGetSpawnOrigin(out var spawnOrigin))
            origin = spawnOrigin;

        EntityCoordinates station = default;
        if (origin == null && !_threat.TryGetRandomStationPoint(out station))
            return false;

        origin ??= station;

        var mapOrigin = _transform.ToMapCoordinates(origin.Value);
        var angle = _random.NextAngle();
        var distance = _random.NextFloat(400f, 600f);
        var spawnMap = new MapCoordinates(mapOrigin.Position + angle.ToVec() * distance, mapOrigin.MapId);
        var mapUid = EntityManager.System<SharedMapSystem>().GetMapOrInvalid(spawnMap.MapId);
        var spawnCoords = new EntityCoordinates(mapUid, spawnMap.Position);

        var whale = Spawn("SpaceLeviathan", spawnCoords);
        _transform.AttachToGridOrMap(whale);
        if (TryComp<PhysicsComponent>(whale, out var physics))
            _physics.SetLinearVelocity(whale, Vector2.Zero, body: physics);

        _threat.SetCurrentWhale(whale);
        _audio.PlayGlobal(SpawnSound, Filter.Broadcast(), true);
        _threat.PlayWhalePresenceCue(whale);
        _threat.LogWhale($"Spawned at {spawnMap}");
        EntitySystem.Get<Content.Server.Chat.Systems.ChatSystem>().DispatchGlobalAnnouncement(Loc.GetString("threat-approaching-announcement"), colorOverride: Color.Gold);
        return true;
    }
}
