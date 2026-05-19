using System.Numerics;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Station.Components;
using Content.Shared.Station.Components;
using Content.Server._Goobstation.SpaceWhale.SpawnLogic;
using Content.Shared.CCVar;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Goobstation.SpaceWhale.Threat;

public sealed partial class WhaleThreatSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedMapSystem _map = default!;

    private const float MinStationSize = 30f;

    private static readonly SoundSpecifier EventStartSound = new SoundPathSpecifier("/Audio/_Goobstation/Ambience/SpaceWhale/leviathan-appear.ogg");
    private static readonly AudioParams WhalePresenceParams = AudioParams.Default.WithVolume(5f).WithMaxDistance(80f);

    private readonly WhaleThreatState _state = new();
    private TimeSpan _nextTick;
    private TimeSpan _nextWhaleAliveCue;
    private TimeSpan _nextNoisePurge;

    public WhaleThreatState State => _state;

    public override void Initialize()
    {
        base.Initialize();
        _nextTick = _timing.CurTime + TimeSpan.FromSeconds(1);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_cfg.GetCVar(CCVars.WhaleEnabled) || _timing.CurTime < _nextTick)
            return;

        _nextTick = _timing.CurTime + TimeSpan.FromSeconds(1);

        if (_timing.CurTime >= _nextNoisePurge)
        {
            PurgeOldNoise();
            _nextNoisePurge = _timing.CurTime + TimeSpan.FromSeconds(5);
        }

        if (!_state.IsAwakened)
            return;

        CleanupCurrentWhale();
        TickThreatMilestones();

        if (_state.CurrentWhale == null && _state.Threat >= _cfg.GetCVar(CCVars.WhaleThreatSpawnAt))
            EntitySystem.Get<SpaceWhaleSpawnSystem>().TrySpawn();

        _state.Threat = ClampThreat(_state.Threat - _cfg.GetCVar(CCVars.WhaleThreatDecay));
    }

    private void TickThreatMilestones()
    {
        if (!_state.WarningAnnounced && _state.Threat >= _cfg.GetCVar(CCVars.WhaleThreatWarningAt))
        {
            _state.WarningAnnounced = true;
            _chat.DispatchGlobalAnnouncement(Loc.GetString("threat-warning-announcement"), colorOverride: Color.Gold);
        }

        if (_state.CurrentWhale is not { } whale || Deleted(whale) || _timing.CurTime < _nextWhaleAliveCue)
            return;

        PlayWhalePresenceCue(whale);
        _nextWhaleAliveCue = _timing.CurTime + GetAliveCueInterval();
    }

    public void Awaken(string reason)
    {
        if (!_cfg.GetCVar(CCVars.WhaleEnabled) || _state.IsAwakened)
            return;

        _state.IsAwakened = true;
        _state.Threat = 0f;
        _state.RecentNoises.Clear();
        _state.WarningAnnounced = false;
        _state.AwakenedAt = _timing.CurTime;

        PlayEventStartCue();
        _chat.DispatchGlobalAnnouncement(Loc.GetString("threat-awakening-announcement"), colorOverride: Color.Gold);
        LogWhale($"Awakening triggered: {reason}");
    }

    public bool WasAwakenedThisTick()
    {
        return _state.IsAwakened && _state.AwakenedAt == _timing.CurTime;
    }

    public void AddThreat(float amount)
    {
        if (!_state.IsAwakened)
            return;

        _state.Threat = ClampThreat(_state.Threat + amount);
    }

    /// <summary>
    /// Add a noise snapshot. Close-by recent noises within aggregation window get merged
    /// into one source (so a burst of fire becomes a single louder pulse).
    /// </summary>
    public void AddNoise(EntityCoordinates coords, float intensity)
    {
        if (intensity <= 0f)
            return;

        if (!coords.IsValid(EntityManager))
            return;

        var mapCoords = _transform.ToMapCoordinates(coords);
        if (mapCoords.MapId == MapId.Nullspace)
            return;

        var now = _timing.CurTime;
        var aggregateRadius = _cfg.GetCVar(CCVars.WhaleNoiseAggregateRadius);
        var aggregateWindow = TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.WhaleNoiseAggregateWindow));

        foreach (var existing in _state.RecentNoises)
        {
            if (existing.MapId != mapCoords.MapId)
                continue;

            if (now - existing.LastUpdatedAt > aggregateWindow)
                continue;

            var existingMap = _transform.ToMapCoordinates(existing.Coords);
            if (existingMap.MapId != mapCoords.MapId)
                continue;

            if ((existingMap.Position - mapCoords.Position).Length() > aggregateRadius)
                continue;

            existing.Intensity += intensity;
            existing.LastUpdatedAt = now;
            existing.Coords = coords;
            return;
        }

        _state.RecentNoises.Add(new WhaleNoiseSnapshot
        {
            Coords = coords,
            Intensity = intensity,
            FirstHeardAt = now,
            LastUpdatedAt = now,
            MapId = mapCoords.MapId,
        });

        var maxEntries = _cfg.GetCVar(CCVars.WhaleNoiseMaxEntries);
        if (_state.RecentNoises.Count > maxEntries)
        {
            _state.RecentNoises.Sort((a, b) => b.Intensity.CompareTo(a.Intensity));
            _state.RecentNoises.RemoveRange(maxEntries, _state.RecentNoises.Count - maxEntries);
        }
    }

    private void PurgeOldNoise()
    {
        var maxAge = TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.WhaleNoiseMaxAge));
        var now = _timing.CurTime;
        _state.RecentNoises.RemoveAll(n => now - n.LastUpdatedAt > maxAge);
    }

    public void ResetAll(string reason = "reset")
    {
        _state.Threat = 0f;
        _state.IsAwakened = false;
        _state.AwakenedAt = TimeSpan.Zero;
        _state.RecentNoises.Clear();
        _state.CurrentWhale = null;
        _state.FarFromStationSince.Clear();
        _state.WarningAnnounced = false;
        _nextWhaleAliveCue = TimeSpan.Zero;
        LogWhale($"State reset: {reason}");
    }

    public void SetThreat(float value)
    {
        _state.Threat = ClampThreat(value);
    }

    public void SetCurrentWhale(EntityUid? whale)
    {
        _state.CurrentWhale = whale;
        if (whale != null)
            _state.IsAwakened = true;

        _nextWhaleAliveCue = whale == null
            ? TimeSpan.Zero
            : _timing.CurTime + GetAliveCueInterval();
    }

    private void CleanupCurrentWhale()
    {
        if (_state.CurrentWhale == null || !Deleted(_state.CurrentWhale.Value))
            return;

        SetCurrentWhale(null);
    }

    private float GetThreatMax()
    {
        return MathF.Max(1f, _cfg.GetCVar(CCVars.WhaleThreatMax));
    }

    private float ClampThreat(float value)
    {
        return Math.Clamp(value, 0f, GetThreatMax());
    }

    private TimeSpan GetAliveCueInterval()
    {
        return TimeSpan.FromSeconds(MathF.Max(1f, _cfg.GetCVar(CCVars.WhaleAliveCueInterval)));
    }

    private void PlayEventStartCue()
    {
        _audio.PlayGlobal(EventStartSound, Filter.Broadcast(), true);
    }

    public void PlayWhalePresenceCue(EntityUid whale)
    {
        _audio.PlayPvs(EventStartSound, whale, WhalePresenceParams);
    }

    public void LogWhale(string message)
    {
        var full = $"[Whale] {message}";
        Logger.InfoS("whale", full);
        if (_cfg.GetCVar(CCVars.WhaleAdminChatSpam))
            _chatManager.SendAdminAnnouncement(full);
    }

    public bool TryGetNearestStation(EntityCoordinates coords, out EntityUid station, out EntityCoordinates stationCoords, out float distance)
    {
        station = default;
        stationCoords = default;
        distance = float.MaxValue;

        if (!coords.IsValid(EntityManager))
            return false;

        var sourceMap = _transform.ToMapCoordinates(coords);
        var query = EntityQueryEnumerator<BecomesStationComponent, MapGridComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var grid, out var xform))
        {
            if (xform.MapID != sourceMap.MapId)
                continue;

            if (grid.LocalAABB.Size.Length() < MinStationSize)
                continue;

            // Должен быть частью реальной станции (не обломок). Обычная
            // станция = grid с StationMemberComponent, обломки этого не имеют.
            if (!HasComp<StationMemberComponent>(uid))
                continue;

            var stationPos = GetStationCenterWorld(xform, grid);
            var radius = grid.LocalAABB.Size.Length() / 2f;
            var current = Math.Max(0f, (sourceMap.Position - stationPos).Length() - radius);
            if (current >= distance)
                continue;

            station = uid;
            stationCoords = new EntityCoordinates(_map.GetMapOrInvalid(sourceMap.MapId), stationPos);
            distance = current;
        }

        return station != default;
    }

    public bool TryGetRandomStationPoint(out EntityCoordinates coords)
    {
        coords = default;
        var count = 0;
        var query = EntityQueryEnumerator<BecomesStationComponent, MapGridComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var grid, out var xform))
        {
            if (grid.LocalAABB.Size.Length() < MinStationSize)
                continue;

            if (!HasComp<StationMemberComponent>(uid))
                continue;

            count++;
            if (_random.Prob(1f / count))
                coords = new EntityCoordinates(_map.GetMapOrInvalid(xform.MapID), GetStationCenterWorld(xform, grid));
        }

        return count > 0;
    }

    private Vector2 GetStationCenterWorld(TransformComponent xform, MapGridComponent grid)
    {
        var worldMatrix = _transform.GetWorldMatrix(xform);
        return Vector2.Transform(grid.LocalAABB.Center, worldMatrix);
    }

    public bool IsLivingHumanoidFarFromStation(EntityUid uid, TransformComponent xform, MobStateComponent mobState, float distance)
    {
        if (!_mobState.IsAlive(uid, mobState))
            return false;

        if (!TryGetNearestStation(xform.Coordinates, out _, out _, out var currentDistance))
            return false;

        return currentDistance > distance;
    }
}
