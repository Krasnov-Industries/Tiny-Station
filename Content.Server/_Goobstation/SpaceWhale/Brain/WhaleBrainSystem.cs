using System.Numerics;
using Content.Server._Goobstation.SpaceWhale.AI;
using Content.Server._Goobstation.SpaceWhale.SpaceWhaleSegment;
using Content.Server._Goobstation.SpaceWhale.Threat;
using Content.Shared.CombatMode;
using Content.Shared.Damage.Components;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.PAI;
using Content.Shared.Physics;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Station.Components;
using Content.Shared.Turrets;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Goobstation.SpaceWhale.Brain;

/// <summary>
/// Простая модель охотника:
///   1. Живая видимая цель, с приоритетом на TopAggressor.
///   2. Движущийся грид с живыми мобами.
///   3. Выход через запомненный пробой, если кит оказался внутри станции.
///   4. Недавний шум, старые точки смерти, брожение около последней активности.
/// </summary>
public sealed partial class WhaleBrainSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedCombatModeSystem _combat = default!;
    [Dependency] private WhaleAbilitySystem _abilities = default!;
    [Dependency] private WhaleThreatSystem _threat = default!;
    [Dependency] private IRobustRandom _random = default!;

    private const float MeleeRange = 3.5f;
    private const float RoarCooldown = 10f;
    private const float RoarTriggerRadius = 8f;
    private const float ExitBreachMargin = 10f;
    private const float DeathScentMergeDistance = 3f;
    private const float ForcedHuntCloseReleaseRadius = 6f;

    /// <summary>
    /// Видимость космического кита: блокируют только непрозрачные объекты
    /// (стены). Окна, решётки, прозрачные двери — пропускают. Кит видит
    /// добычу внутри станции с космоса через иллюминаторы.
    /// </summary>
    private const CollisionGroup SightMask = CollisionGroup.Opaque;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<WhaleBrainComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var brain, out var xform))
        {
            if (now < brain.NextTick)
                continue;

            brain.NextTick = now + TimeSpan.FromSeconds(brain.TickInterval);
            Tick(uid, brain, xform);
        }
    }

    private void Tick(EntityUid whale, WhaleBrainComponent brain, TransformComponent xform)
    {
        _combat.SetInCombatMode(whale, true);
        EnsureForcedHuntClock(brain);

        var target = PickTarget(whale, brain, xform);
        brain.CurrentTarget = target.Entity;
        brain.CurrentBehavior = target.Behavior;

        TryComp<TailedEntityComponent>(whale, out var tail);

        var huntingMove = UsesHuntingMotion(target.Behavior);
        var targetSpeed = GetDesiredSpeed(brain, target);
        brain.LastDesiredSpeed = targetSpeed;
        if (huntingMove && brain.CurrentSpeed < brain.CruiseSpeed)
            brain.CurrentSpeed = brain.CruiseSpeed;

        var delta = targetSpeed - brain.CurrentSpeed;
        var accel = delta < 0f ? brain.SpeedBrakeAccel : brain.SpeedAccel;
        var maxStep = accel * brain.TickInterval;
        if (MathF.Abs(delta) <= maxStep)
            brain.CurrentSpeed = targetSpeed;
        else
            brain.CurrentSpeed += MathF.Sign(delta) * maxStep;
        brain.CurrentSpeed = Math.Clamp(brain.CurrentSpeed, 0f, GetMaxConfiguredSpeed(brain));

        if (tail != null)
        {
            tail.IsHunting = huntingMove;
            tail.IsCarefulMovement = target.Coords != null && !huntingMove;
            tail.OverrideBaseSpeed = brain.CurrentSpeed;
        }

        MoveTo(whale, xform, target.Coords);

        if (target.Entity is { } victim && InMeleeRange(xform, victim) && ShouldBiteTarget(whale, victim, target.Behavior))
        {
            TryBite(whale, victim);
        }

        var hostileTarget = target.Entity != null && target.Behavior != WhaleBehavior.ConsumeTarget;
        if (hostileTarget || HasAnyLivingNear(whale, xform, RoarTriggerRadius))
            _abilities.TryRoar(whale, RoarCooldown);
    }

    private static bool UsesHuntingMotion(WhaleBehavior behavior)
    {
        return behavior is WhaleBehavior.HuntMob or WhaleBehavior.ForcedMapHunt or WhaleBehavior.AttackEntity or WhaleBehavior.AttackMovingGrid;
    }

    private static float GetDesiredSpeed(WhaleBrainComponent brain, PickResult target)
    {
        return target.Behavior switch
        {
            WhaleBehavior.HuntMob or WhaleBehavior.ForcedMapHunt or WhaleBehavior.AttackEntity or WhaleBehavior.AttackMovingGrid => brain.HuntingSpeed,
            WhaleBehavior.ConsumeTarget => brain.DeathScentSpeed,
            WhaleBehavior.ExitBreach => brain.ExitBreachSpeed,
            WhaleBehavior.InvestigateNoise => target.Stimulus >= brain.AlertNoiseIntensity
                ? brain.AlertNoiseSpeed
                : brain.InvestigateSpeed,
            WhaleBehavior.FollowDeathScent => brain.DeathScentSpeed,
            WhaleBehavior.Lurk => brain.LurkSpeed,
            WhaleBehavior.Idle => 0f,
            _ => brain.CruiseSpeed,
        };
    }

    private static float GetMaxConfiguredSpeed(WhaleBrainComponent brain)
    {
        return MathF.Max(
            brain.HuntingSpeed,
            MathF.Max(
                brain.AlertNoiseSpeed,
                MathF.Max(
                    brain.CruiseSpeed,
                    MathF.Max(
                        brain.ExitBreachSpeed,
                        MathF.Max(
                            brain.InvestigateSpeed,
                            MathF.Max(brain.DeathScentSpeed, brain.LurkSpeed))))));
    }

    private readonly record struct PickResult(EntityUid? Entity, EntityCoordinates? Coords, WhaleBehavior Behavior, float Stimulus = 0f);

    private PickResult PickTarget(EntityUid whale, WhaleBrainComponent brain, TransformComponent xform)
    {
        var coords = xform.Coordinates;
        PurgeExpiredDeathScents(brain);

        if (TryPickForcedMapMob(whale, brain, xform, out var forcedTarget, out var forcedCoords))
        {
            RememberActivity(brain, forcedCoords);
            brain.LastPickReason = "forced-map-hunt";
            brain.LastVisibleMobs = 0;
            brain.LastInRangeMobs = 0;
            return new PickResult(forcedTarget, forcedCoords, WhaleBehavior.ForcedMapHunt);
        }

        if (TryPickVisibleMob(whale, brain, coords, out var visible, out var stats))
        {
            RememberActivity(brain, Transform(visible).Coordinates);
            brain.LastPickReason = "hunt-mob";
            brain.LastVisibleMobs = stats.Visible;
            brain.LastInRangeMobs = stats.InRange;
            return new PickResult(visible, Transform(visible).Coordinates, WhaleBehavior.HuntMob);
        }

        brain.LastVisibleMobs = stats.Visible;
        brain.LastInRangeMobs = stats.InRange;

        if (TryPickVisibleConsumeTarget(whale, brain, coords, out var consume))
        {
            var targetCoords = Transform(consume).Coordinates;
            RememberActivity(brain, targetCoords);
            brain.LastPickReason = "consume-target";
            return new PickResult(consume, targetCoords, WhaleBehavior.ConsumeTarget);
        }

        if (TryPickVisibleAttackEntity(whale, brain, coords, out var attack))
        {
            var targetCoords = Transform(attack).Coordinates;
            RememberActivity(brain, targetCoords);
            brain.LastPickReason = "attack-entity";
            return new PickResult(attack, targetCoords, WhaleBehavior.AttackEntity);
        }

        // Движущийся грид (шатл / спасательная капсула / прочая жизнь в космосе).
        // Глухой шатл без окон не пропускает свет, но движение его массы кит
        // ощущает — есть кого там пощупать.
        if (TryPickMovingGrid(whale, brain, xform, out var gridCoords))
        {
            RememberActivity(brain, gridCoords);
            brain.LastPickReason = "moving-grid";
            return new PickResult(null, gridCoords, WhaleBehavior.AttackMovingGrid);
        }

        if (TryPickBreachExit(brain, xform, out var breach))
        {
            brain.LastPickReason = "exit-breach";
            return new PickResult(null, breach, WhaleBehavior.ExitBreach);
        }

        if (TryPickNoise(brain, xform, out var noise, out var noiseIntensity))
        {
            brain.LastPickReason = "noise";
            return new PickResult(null, noise, WhaleBehavior.InvestigateNoise, noiseIntensity);
        }

        if (TryPickDeathScent(brain, xform, out var scent))
        {
            brain.LastPickReason = "death-scent";
            return new PickResult(null, scent, WhaleBehavior.FollowDeathScent);
        }

        if (TryPickLurk(brain, xform, out var lurk))
        {
            brain.LastPickReason = "lurk";
            return new PickResult(null, lurk, WhaleBehavior.Lurk);
        }

        brain.LastPickReason = "idle";
        return new PickResult(null, null, WhaleBehavior.Idle);
    }

    private void EnsureForcedHuntClock(WhaleBrainComponent brain)
    {
        if (brain.NextForcedHuntAt != TimeSpan.Zero)
            return;

        var now = _timing.CurTime;
        brain.LastKillAt = now;
        brain.NextForcedHuntAt = now + TimeSpan.FromSeconds(brain.ForcedHuntNoKillDelay);
    }

    private bool TryPickForcedMapMob(
        EntityUid whale,
        WhaleBrainComponent brain,
        TransformComponent xform,
        out EntityUid target,
        out EntityCoordinates coords)
    {
        target = default;
        coords = default;

        var now = _timing.CurTime;
        if (now < brain.NextForcedHuntAt)
            return false;

        if (brain.ForcedHuntTarget is { } existing)
        {
            if (IsValidWhaleMobTarget(whale, existing) &&
                TryComp<TransformComponent>(existing, out var existingXform) &&
                TryGetDistance(xform.Coordinates, existingXform.Coordinates, out var existingDistance))
            {
                if (!CanReleaseForcedHunt(whale, brain, existing, existingDistance))
                {
                    target = existing;
                    coords = existingXform.Coordinates;
                    return true;
                }

                brain.ForcedHuntTarget = null;
                brain.NextForcedHuntAt = now + TimeSpan.FromSeconds(brain.ForcedHuntNoKillDelay);
                brain.LastPickReason = "forced-map-hunt-release";
                return false;
            }

            brain.ForcedHuntTarget = null;
        }

        if (!TryFindNearestMapMob(whale, xform, out target, out coords, out var distance))
        {
            brain.NextForcedHuntAt = now + TimeSpan.FromSeconds(brain.ForcedHuntNoKillDelay);
            brain.LastPickReason = "forced-map-hunt-no-target";
            return false;
        }

        if (CanReleaseForcedHunt(whale, brain, target, distance))
        {
            brain.NextForcedHuntAt = now + TimeSpan.FromSeconds(brain.ForcedHuntNoKillDelay);
            brain.LastPickReason = "forced-map-hunt-release";
            return false;
        }

        brain.ForcedHuntTarget = target;
        return true;
    }

    private bool CanReleaseForcedHunt(EntityUid whale, WhaleBrainComponent brain, EntityUid target, float distance)
    {
        if (distance > brain.ForcedHuntReleaseRadius)
            return false;

        return distance <= ForcedHuntCloseReleaseRadius || HasLineOfSight(whale, target, brain.SightRadius);
    }

    private bool TryFindNearestMapMob(
        EntityUid whale,
        TransformComponent xform,
        out EntityUid target,
        out EntityCoordinates coords,
        out float distance)
    {
        target = default;
        coords = default;
        distance = float.MaxValue;

        var whaleMap = _transform.ToMapCoordinates(xform.Coordinates);
        if (whaleMap.MapId == MapId.Nullspace)
            return false;

        var query = EntityQueryEnumerator<MobStateComponent, MobMoverComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out _, out var targetXform))
        {
            if (!IsValidWhaleMobTarget(whale, uid))
                continue;

            var targetMap = _transform.ToMapCoordinates(targetXform.Coordinates);
            if (targetMap.MapId != whaleMap.MapId)
                continue;

            var currentDistance = (targetMap.Position - whaleMap.Position).Length();
            if (currentDistance >= distance)
                continue;

            target = uid;
            coords = targetXform.Coordinates;
            distance = currentDistance;
        }

        return target != default;
    }

    private bool TryPickBreachExit(WhaleBrainComponent brain, TransformComponent xform, out EntityCoordinates coords)
    {
        coords = default;

        if (!IsOnStationGrid(xform))
            return false;

        if (brain.LastBreachCoords is not { } breach || !breach.IsValid(EntityManager))
            return false;

        if (!TryGetDistance(xform.Coordinates, breach, out var distance))
            return false;

        if (distance <= brain.BreachExitArrivalRadius)
        {
            brain.LastBreachCoords = null;
            return false;
        }

        coords = breach;
        return true;
    }

    private bool TryPickNoise(WhaleBrainComponent brain, TransformComponent xform, out EntityCoordinates coords, out float intensity)
    {
        coords = default;
        intensity = 0f;
        var now = _timing.CurTime;

        if (brain.InvestigateCoords is { } current)
        {
            if (now < brain.InvestigateUntil &&
                current.IsValid(EntityManager) &&
                TryGetDistance(xform.Coordinates, current, out var currentDistance) &&
                currentDistance > brain.InvestigateArrivalRadius &&
                IsWithinNoiseInterest(currentDistance, brain))
            {
                coords = current;
                intensity = brain.ActiveNoiseIntensity;
                return true;
            }

            brain.InvestigateCoords = null;
            brain.ActiveNoiseIntensity = 0f;
        }

        var source = _transform.ToMapCoordinates(xform.Coordinates);
        if (source.MapId == MapId.Nullspace)
            return false;

        WhaleNoiseSnapshot? best = null;
        var bestScore = float.MinValue;

        foreach (var noise in _threat.State.RecentNoises)
        {
            if (noise.MapId != source.MapId || !noise.Coords.IsValid(EntityManager))
                continue;

            var noiseMap = _transform.ToMapCoordinates(noise.Coords);
            var distance = (noiseMap.Position - source.Position).Length();
            if (!IsWithinNoiseInterest(distance, brain) || distance <= brain.InvestigateArrivalRadius)
                continue;

            var age = (float) (now - noise.LastUpdatedAt).TotalSeconds;
            var score = noise.Intensity - distance * 0.05f - age * 0.25f;

            if (score <= bestScore)
                continue;

            bestScore = score;
            best = noise;
        }

        if (best == null)
            return false;

        coords = best.Coords;
        intensity = best.Intensity;
        brain.InvestigateCoords = coords;
        brain.InvestigateUntil = now + TimeSpan.FromSeconds(brain.InvestigateDuration);
        brain.ActiveNoiseIntensity = best.Intensity;
        RememberActivity(brain, coords);
        return true;
    }

    private static bool IsWithinNoiseInterest(float distance, WhaleBrainComponent brain)
    {
        if (brain.NoiseInterestRadius <= 0f)
            return true;

        return distance <= brain.NoiseInterestRadius;
    }

    private bool TryPickDeathScent(WhaleBrainComponent brain, TransformComponent xform, out EntityCoordinates coords)
    {
        coords = default;
        var now = _timing.CurTime;

        if (brain.ActiveDeathScentCoords is { } active)
        {
            if (now < brain.ActiveDeathScentUntil &&
                active.IsValid(EntityManager) &&
                TryGetDistance(xform.Coordinates, active, out var activeDistance) &&
                activeDistance > brain.DeathScentArrivalRadius)
            {
                coords = active;
                return true;
            }

            brain.ActiveDeathScentCoords = null;
        }

        if (brain.DeathScents.Count < 2)
            return false;

        var newest = 0;
        for (var i = 1; i < brain.DeathScents.Count; i++)
        {
            if (brain.DeathScents[i].CreatedAt > brain.DeathScents[newest].CreatedAt)
                newest = i;
        }

        var candidates = 0;
        for (var i = 0; i < brain.DeathScents.Count; i++)
        {
            if (i == newest)
                continue;

            if (brain.DeathScents[i].Coords.IsValid(EntityManager) &&
                TryGetDistance(xform.Coordinates, brain.DeathScents[i].Coords, out var distance) &&
                distance > brain.DeathScentArrivalRadius)
                candidates++;
        }

        if (candidates == 0)
            return false;

        var pick = _random.Next(candidates);
        for (var i = 0; i < brain.DeathScents.Count; i++)
        {
            if (i == newest)
                continue;

            var scent = brain.DeathScents[i].Coords;
            if (!scent.IsValid(EntityManager) ||
                !TryGetDistance(xform.Coordinates, scent, out var distance) ||
                distance <= brain.DeathScentArrivalRadius)
                continue;

            if (pick-- != 0)
                continue;

            coords = scent;
            brain.ActiveDeathScentCoords = scent;
            brain.ActiveDeathScentUntil = now + TimeSpan.FromSeconds(brain.DeathScentFollowDuration);
            return true;
        }

        return false;
    }

    private bool TryPickLurk(WhaleBrainComponent brain, TransformComponent xform, out EntityCoordinates coords)
    {
        coords = default;
        var now = _timing.CurTime;

        if (brain.LastActivityCoords is not { } activity ||
            !activity.IsValid(EntityManager) ||
            !SameMap(xform.Coordinates, activity))
            return false;

        if (brain.LurkCoords is { } current &&
            now < brain.NextLurkPick &&
            current.IsValid(EntityManager) &&
            TryGetDistance(xform.Coordinates, current, out var currentDistance) &&
            currentDistance > brain.LurkArrivalRadius)
        {
            coords = current;
            return true;
        }

        var activityMap = _transform.ToMapCoordinates(activity);
        var minRadius = MathF.Min(brain.LurkMinRadius, brain.LurkMaxRadius);
        var maxRadius = MathF.Max(brain.LurkMinRadius, brain.LurkMaxRadius);
        var radius = maxRadius <= minRadius ? minRadius : _random.NextFloat(minRadius, maxRadius);
        var angle = _random.NextFloat(0f, MathF.PI * 2f);
        var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;

        coords = _transform.ToCoordinates(new MapCoordinates(activityMap.Position + offset, activityMap.MapId));
        brain.LurkCoords = coords;
        brain.NextLurkPick = now + TimeSpan.FromSeconds(brain.LurkPickInterval);
        return coords.IsValid(EntityManager);
    }

    private record struct MobSearchStats(int InRange, int Visible);

    /// <summary>
    /// Ближайший живой моб в SightRadius с прямой видимостью.
    /// TopAggressor приоритетнее остальных, если тоже видим.
    /// </summary>
    private bool TryPickVisibleMob(
        EntityUid whale,
        WhaleBrainComponent brain,
        EntityCoordinates origin,
        out EntityUid best,
        out MobSearchStats stats)
    {
        best = default;
        stats = default;
        var topAggressor = TryComp<WhaleMemoryComponent>(whale, out var mem) ? mem.TopAggressor : null;

        EntityUid? top = null;
        var topDist = float.MaxValue;
        var bestDist = float.MaxValue;
        var inRange = 0;
        var visible = 0;

        foreach (var cand in _lookup.GetEntitiesInRange<MobStateComponent>(origin, brain.SightRadius))
        {
            var target = cand.Owner;
            if (!IsValidWhaleMobTarget(whale, target))
                continue;

            if (!origin.TryDistance(EntityManager, Transform(target).Coordinates, out var d) || d > brain.SightRadius)
                continue;

            inRange++;

            if (!HasLineOfSight(whale, target, brain.SightRadius))
                continue;

            visible++;

            if (target == topAggressor)
            {
                if (d < topDist) { topDist = d; top = target; }
            }
            else if (d < bestDist)
            {
                bestDist = d;
                best = target;
            }
        }

        stats = new MobSearchStats(inRange, visible);
        if (top is { } t) { best = t; return true; }
        return bestDist < float.MaxValue;
    }

    private bool TryPickVisibleConsumeTarget(
        EntityUid whale,
        WhaleBrainComponent brain,
        EntityCoordinates origin,
        out EntityUid best)
    {
        best = default;
        var bestDist = float.MaxValue;

        foreach (var candidate in _lookup.GetEntitiesInRange<MobStateComponent>(origin, brain.SightRadius))
        {
            var target = candidate.Owner;
            if (!IsValidWhaleConsumeMobTarget(whale, target, candidate.Comp))
                continue;

            if (!TryPickVisibleEntity(origin, whale, target, brain.SightRadius, ref bestDist))
                continue;

            best = target;
        }

        foreach (var candidate in _lookup.GetEntitiesInRange<PAIComponent>(origin, brain.SightRadius))
        {
            var target = candidate.Owner;
            if (!IsValidWhalePaiConsumeTarget(whale, target))
                continue;

            if (!TryPickVisibleEntity(origin, whale, target, brain.SightRadius, ref bestDist))
                continue;

            best = target;
        }

        return best != default;
    }

    private bool TryPickVisibleAttackEntity(
        EntityUid whale,
        WhaleBrainComponent brain,
        EntityCoordinates origin,
        out EntityUid best)
    {
        best = default;
        var bestDist = float.MaxValue;

        foreach (var candidate in _lookup.GetEntitiesInRange<DamageableComponent>(origin, brain.SightRadius))
        {
            var target = candidate.Owner;
            if (!IsValidWhaleAttackTarget(whale, target))
                continue;

            if (!TryPickVisibleEntity(origin, whale, target, brain.SightRadius, ref bestDist))
                continue;

            best = target;
        }

        return best != default;
    }

    private bool TryPickVisibleEntity(
        EntityCoordinates origin,
        EntityUid whale,
        EntityUid target,
        float radius,
        ref float bestDist)
    {
        if (!origin.TryDistance(EntityManager, Transform(target).Coordinates, out var distance) || distance > radius)
            return false;

        if (distance >= bestDist)
            return false;

        if (!HasLineOfSight(whale, target, radius))
            return false;

        bestDist = distance;
        return true;
    }

    private bool IsValidWhaleConsumeMobTarget(EntityUid whale, EntityUid target, MobStateComponent mobState)
    {
        if (!IsBaseValidWhaleTarget(whale, target))
            return false;

        if (HasComp<ItemComponent>(target) || HasComp<WhaleEatenCorpseComponent>(target))
            return false;

        return _mobState.IsIncapacitated(target, mobState);
    }

    private bool IsValidWhalePaiConsumeTarget(EntityUid whale, EntityUid target)
    {
        if (!IsBaseValidWhaleTarget(whale, target))
            return false;

        return HasComp<PAIComponent>(target) && !HasComp<WhaleEatenCorpseComponent>(target);
    }

    private bool IsValidWhaleAttackTarget(EntityUid whale, EntityUid target)
    {
        if (!IsBaseValidWhaleTarget(whale, target))
            return false;

        if (HasComp<ItemComponent>(target) ||
            HasComp<WhaleEatenCorpseComponent>(target) ||
            HasComp<MobStateComponent>(target) ||
            !HasComp<DamageableComponent>(target))
        {
            return false;
        }

        if (HasComp<DeployableTurretComponent>(target) ||
            HasComp<StationAiTurretComponent>(target) ||
            HasComp<TurretTargetSettingsComponent>(target) ||
            HasComp<StationAiCoreComponent>(target))
        {
            return true;
        }

        var prototypeId = Prototype(target)?.ID;
        return prototypeId != null &&
               (prototypeId.StartsWith("WeaponTurret", StringComparison.Ordinal) ||
                prototypeId.StartsWith("WeaponEnergyTurret", StringComparison.Ordinal));
    }

    private bool IsValidWhaleMobTarget(EntityUid whale, EntityUid target)
    {
        if (!IsWhaleMobLikeEntity(whale, target))
            return false;

        return TryComp<MobStateComponent>(target, out var mobState) && _mobState.IsAlive(target, mobState);
    }

    private bool CountsAsWhaleKillTarget(EntityUid whale, EntityUid target)
    {
        return IsWhaleMobLikeEntity(whale, target) && HasComp<MobStateComponent>(target);
    }

    private bool IsWhaleMobLikeEntity(EntityUid whale, EntityUid target)
    {
        if (!IsBaseValidWhaleTarget(whale, target))
            return false;

        // pAI and similar living items have MobState for ghost-role logic, but
        // they are still items, not mobs for whale target selection.
        if (HasComp<ItemComponent>(target))
            return false;

        return HasComp<MobMoverComponent>(target);
    }

    private bool IsBaseValidWhaleTarget(EntityUid whale, EntityUid target)
    {
        if (target == whale || Deleted(target))
            return false;

        return !HasComp<WhaleSpawnedByComponent>(target) && !HasComp<SpaceWhaleSegmentComponent>(target);
    }

    /// <summary>
    /// Ближайший движущийся грид (шатл, капсула, не-станция) в SightRadius.
    /// Грид с (почти) нулевой velocity игнорируется — припаркованные/станции
    /// не интересны как цель. Свой собственный грид (если кит на нём) тоже.
    /// </summary>
    private bool TryPickMovingGrid(
        EntityUid whale,
        WhaleBrainComponent brain,
        TransformComponent xform,
        out EntityCoordinates best)
    {
        best = default;
        var bestDist = float.MaxValue;
        var ownGrid = xform.GridUid;

        foreach (var grid in _lookup.GetEntitiesInRange<MapGridComponent>(xform.Coordinates, brain.SightRadius))
        {
            if (grid.Owner == ownGrid)
                continue;

            if (HasComp<StationMemberComponent>(grid.Owner))
                continue;

            if (!TryComp<PhysicsComponent>(grid.Owner, out var phys))
                continue;
            // Меньше 0.5 тайла/с = "стоит". Не цель.
            if (phys.LinearVelocity.LengthSquared() < 0.25f)
                continue;

            // Грид без живых мобов — обломок/мусор/спам-капсула. Не интересно.
            if (!HasLivingMobsOnGrid(whale, grid.Owner))
                continue;

            var gridCoords = Transform(grid.Owner).Coordinates;
            if (!xform.Coordinates.TryDistance(EntityManager, gridCoords, out var d) || d > brain.SightRadius)
                continue;

            if (d < bestDist)
            {
                bestDist = d;
                best = gridCoords;
            }
        }

        return bestDist < float.MaxValue;
    }

    private bool HasLivingMobsOnGrid(EntityUid whale, EntityUid gridUid)
    {
        var enumerator = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (enumerator.MoveNext(out var uid, out _, out var xf))
        {
            if (xf.GridUid != gridUid)
                continue;

            if (!IsValidWhaleMobTarget(whale, uid))
                continue;

            return true;
        }
        return false;
    }

    private bool HasLineOfSight(EntityUid whale, EntityUid target, float radius)
    {
        // Сегменты самого кита — большие фикстуры вокруг головы. Без их
        // исключения луч может упереться в сегмент при вплотную стоящей жертве.
        return _interaction.InRangeUnobstructed(
            whale,
            new Entity<TransformComponent?>(target, null),
            radius,
            SightMask,
            predicate: IsOwnSegment);
    }

    private bool IsOwnSegment(EntityUid entity)
    {
        return HasComp<SpaceWhaleSegmentComponent>(entity);
    }

    private bool ShouldBiteTarget(EntityUid whale, EntityUid target, WhaleBehavior behavior)
    {
        return behavior switch
        {
            WhaleBehavior.HuntMob or WhaleBehavior.ForcedMapHunt => IsValidWhaleMobTarget(whale, target),
            WhaleBehavior.AttackEntity => IsValidWhaleAttackTarget(whale, target),
            _ => false,
        };
    }

    private bool InMeleeRange(TransformComponent whaleXform, EntityUid target)
    {
        return whaleXform.Coordinates.TryDistance(EntityManager, Transform(target).Coordinates, out var dist) &&
               dist <= MeleeRange;
    }

    private bool HasAnyLivingNear(EntityUid whale, TransformComponent xform, float radius)
    {
        foreach (var candidate in _lookup.GetEntitiesInRange<MobStateComponent>(xform.Coordinates, radius))
        {
            if (IsValidWhaleMobTarget(whale, candidate.Owner))
                return true;
        }

        return false;
    }

    private bool IsOnStationGrid(TransformComponent xform)
    {
        return xform.GridUid is { } grid && HasComp<StationMemberComponent>(grid);
    }

    private bool SameMap(EntityCoordinates a, EntityCoordinates b)
    {
        if (!a.IsValid(EntityManager) || !b.IsValid(EntityManager))
            return false;

        return _transform.ToMapCoordinates(a).MapId == _transform.ToMapCoordinates(b).MapId;
    }

    private bool TryGetDistance(EntityCoordinates a, EntityCoordinates b, out float distance)
    {
        distance = default;

        if (!a.IsValid(EntityManager) || !b.IsValid(EntityManager))
            return false;

        var aMap = _transform.ToMapCoordinates(a);
        var bMap = _transform.ToMapCoordinates(b);
        if (aMap.MapId != bMap.MapId || aMap.MapId == MapId.Nullspace)
            return false;

        distance = (bMap.Position - aMap.Position).Length();
        return true;
    }

    private void MoveTo(EntityUid whale, TransformComponent xform, EntityCoordinates? target)
    {
        if (target == null)
        {
            Stop(whale);
            return;
        }

        var ownerMap = _transform.ToMapCoordinates(xform.Coordinates);
        var targetMap = _transform.ToMapCoordinates(target.Value);
        if (ownerMap.MapId != targetMap.MapId || ownerMap.MapId == MapId.Nullspace)
        {
            Stop(whale);
            return;
        }

        var delta = targetMap.Position - ownerMap.Position;
        var distance = delta.Length();
        if (distance < 1.2f)
        {
            Stop(whale);
            return;
        }

        var direction = delta / distance;

        // Берём актуальную скорость от Brain (CurrentSpeed) — она плавно
        // меняется между Cruise и Hunting в зависимости от выбранного поведения.
        var speed = TryComp<WhaleBrainComponent>(whale, out var brainComp)
            ? brainComp.CurrentSpeed
            : TryComp<MovementSpeedModifierComponent>(whale, out var modifier)
                ? modifier.CurrentSprintSpeed
                : 5f;

        TryComp<TailedEntityComponent>(whale, out var tail);
        // При целенаправленном движении голова не тормозит из-за хвоста —
        // иначе застрявшие в стенах сегменты не дают выйти или догнать цель.
        if (tail != null && !tail.IsHunting)
            speed *= Math.Clamp(tail.HeadSpeedMultiplier, 0f, 1f);

        if (TryComp<PhysicsComponent>(whale, out var physics))
            _physics.SetLinearVelocity(whale, speed <= 0.05f ? Vector2.Zero : direction * speed, body: physics);

        // Желаемое направление взгляда — TailedEntitySystem каждый кадр
        // плавно доворачивает rotation сюда, без резких "щелчков" между тиками.
        if (tail != null)
        {
            tail.DesiredFacing = direction;
            tail.BrainDesiresMovement = speed > 0.05f;
        }
        else
        {
            _transform.SetWorldRotation(whale, direction.ToWorldAngle());
        }
    }

    private void Stop(EntityUid whale)
    {
        if (TryComp<PhysicsComponent>(whale, out var physics))
            _physics.SetLinearVelocity(whale, Vector2.Zero, body: physics);
        if (TryComp<TailedEntityComponent>(whale, out var tail))
        {
            tail.BrainDesiresMovement = false;
            tail.IsCarefulMovement = false;
            tail.OverrideBaseSpeed = 0f;
        }
    }

    private void TryBite(EntityUid whale, EntityUid target)
    {
        if (!TryComp<MeleeWeaponComponent>(whale, out var weapon))
            return;

        if (weapon.NextAttack > _timing.CurTime)
            return;

        _melee.AttemptLightAttack(whale, whale, weapon, target);

        if (TryComp<MobStateComponent>(target, out var mob) && mob.CurrentState == MobState.Dead)
        {
            if (TryComp<WhaleBrainComponent>(whale, out var brain) && CountsAsWhaleKillTarget(whale, target))
                RegisterKill(brain);

            RememberDeathScent(whale, Transform(target).Coordinates);
        }
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (_threat.State.CurrentWhale is not { } whale || !Exists(whale))
            return;

        if (!TryComp<WhaleBrainComponent>(whale, out var brain))
            return;

        if (args.Target == whale || HasComp<SpaceWhaleSegmentComponent>(args.Target))
            return;

        var causedByWhale = args.Origin == whale;
        if (!causedByWhale &&
            args.Origin is { } origin &&
            TryComp<SpaceWhaleSegmentComponent>(origin, out var segment))
        {
            causedByWhale = segment.Whale == whale;
        }

        if (causedByWhale && CountsAsWhaleKillTarget(whale, args.Target))
            RegisterKill(brain);

        var targetCoords = Transform(args.Target).Coordinates;
        var whaleCoords = Transform(whale).Coordinates;
        var wasTargeted = brain.CurrentTarget == args.Target;
        var wasClose = whaleCoords.TryDistance(EntityManager, targetCoords, out var dist) && dist <= MeleeRange + 2f;

        if (!causedByWhale && !wasTargeted && !wasClose)
            return;

        RememberDeathScent(whale, targetCoords, brain);
    }

    private void RegisterKill(WhaleBrainComponent brain)
    {
        var now = _timing.CurTime;
        brain.LastKillAt = now;
        brain.NextForcedHuntAt = now + TimeSpan.FromSeconds(brain.ForcedHuntNoKillDelay);
        brain.ForcedHuntTarget = null;
    }

    public void RememberActivity(EntityUid whale, EntityCoordinates coords, WhaleBrainComponent? brain = null)
    {
        if (!Resolve(whale, ref brain, false))
            return;

        RememberActivity(brain, coords);
    }

    public void RememberDeathScent(EntityUid whale, EntityCoordinates coords, WhaleBrainComponent? brain = null)
    {
        if (!Resolve(whale, ref brain, false) || !coords.IsValid(EntityManager))
            return;

        PurgeExpiredDeathScents(brain);
        RememberActivity(brain, coords);
        if (TryMergeDeathScent(brain, coords))
            return;

        brain.DeathScents.Add(new WhaleDeathScent
        {
            Coords = coords,
            CreatedAt = _timing.CurTime,
        });
        TrimDeathScents(brain);
    }

    private bool TryMergeDeathScent(WhaleBrainComponent brain, EntityCoordinates coords)
    {
        for (var i = 0; i < brain.DeathScents.Count; i++)
        {
            var scent = brain.DeathScents[i];
            if (!scent.Coords.TryDistance(EntityManager, coords, out var distance) ||
                distance > DeathScentMergeDistance)
                continue;

            scent.Coords = coords;
            scent.CreatedAt = _timing.CurTime;
            return true;
        }

        return false;
    }

    public void RememberBreach(
        EntityUid whale,
        EntityCoordinates coords,
        EntityUid? stationGrid = null,
        bool preserveExisting = false,
        WhaleBrainComponent? brain = null)
    {
        if (!Resolve(whale, ref brain, false) || !coords.IsValid(EntityManager))
            return;

        if (preserveExisting &&
            brain.LastBreachCoords is { } existing &&
            existing.IsValid(EntityManager) &&
            SameMap(existing, coords) &&
            (stationGrid is not { } preserveGrid || IsOutsideStationGridBounds(existing, preserveGrid)))
        {
            RememberActivity(brain, coords);
            return;
        }

        var breach = coords;
        if (stationGrid is { } grid && TryGetStationExitCoords(coords, grid, out var exit))
            breach = exit;

        brain.LastBreachCoords = breach;
        RememberActivity(brain, coords);
    }

    private void RememberActivity(WhaleBrainComponent brain, EntityCoordinates coords)
    {
        if (!coords.IsValid(EntityManager))
            return;

        brain.LastActivityCoords = coords;
    }

    private void PurgeExpiredDeathScents(WhaleBrainComponent brain)
    {
        var cutoff = _timing.CurTime - TimeSpan.FromSeconds(brain.DeathScentTtl);
        for (var i = brain.DeathScents.Count - 1; i >= 0; i--)
        {
            var scent = brain.DeathScents[i];
            if (scent.CreatedAt < cutoff || !scent.Coords.IsValid(EntityManager))
                brain.DeathScents.RemoveAt(i);
        }

        TrimDeathScents(brain);
    }

    private void TrimDeathScents(WhaleBrainComponent brain)
    {
        while (brain.DeathScents.Count > Math.Max(0, brain.MaxDeathScents))
        {
            var oldest = 0;
            for (var i = 1; i < brain.DeathScents.Count; i++)
            {
                if (brain.DeathScents[i].CreatedAt < brain.DeathScents[oldest].CreatedAt)
                    oldest = i;
            }

            brain.DeathScents.RemoveAt(oldest);
        }
    }

    private bool TryGetStationExitCoords(EntityCoordinates from, EntityUid stationGrid, out EntityCoordinates coords)
    {
        coords = default;

        if (!TryComp<MapGridComponent>(stationGrid, out var grid) ||
            !TryComp<TransformComponent>(stationGrid, out var gridXform))
            return false;

        var fromMap = _transform.ToMapCoordinates(from);
        if (fromMap.MapId == MapId.Nullspace || fromMap.MapId != gridXform.MapID)
            return false;

        var gridMatrix = _transform.GetWorldMatrix(gridXform);
        if (!Matrix3x2.Invert(gridMatrix, out var invGridMatrix))
            return false;

        var center = grid.LocalAABB.Center;
        var fromLocal = Vector2.Transform(fromMap.Position, invGridMatrix);
        var direction = fromLocal - center;
        if (direction.LengthSquared() < 0.01f)
            direction = Vector2.UnitY;

        direction = Vector2.Normalize(direction);
        var exitBounds = grid.LocalAABB.Enlarged(ExitBreachMargin);
        var distanceToEdge = GetRayBoxExitDistance(center, direction, exitBounds);
        if (distanceToEdge <= 0f || float.IsInfinity(distanceToEdge) || float.IsNaN(distanceToEdge))
            return false;

        var exitLocal = center + direction * distanceToEdge;
        var exitPosition = Vector2.Transform(exitLocal, gridMatrix);
        coords = _transform.ToCoordinates(new MapCoordinates(exitPosition, fromMap.MapId));
        return coords.IsValid(EntityManager);
    }

    private bool IsOutsideStationGridBounds(EntityCoordinates coords, EntityUid stationGrid)
    {
        if (!TryComp<MapGridComponent>(stationGrid, out var grid) ||
            !TryComp<TransformComponent>(stationGrid, out var gridXform))
            return false;

        var map = _transform.ToMapCoordinates(coords);
        if (map.MapId == MapId.Nullspace || map.MapId != gridXform.MapID)
            return false;

        var gridMatrix = _transform.GetWorldMatrix(gridXform);
        if (!Matrix3x2.Invert(gridMatrix, out var invGridMatrix))
            return false;

        var local = Vector2.Transform(map.Position, invGridMatrix);
        return !grid.LocalAABB.Enlarged(1f).Contains(local);
    }

    private static float GetRayBoxExitDistance(Vector2 origin, Vector2 direction, Box2 box)
    {
        var tx = direction.X switch
        {
            > 0f => (box.Right - origin.X) / direction.X,
            < 0f => (box.Left - origin.X) / direction.X,
            _ => float.PositiveInfinity,
        };

        var ty = direction.Y switch
        {
            > 0f => (box.Top - origin.Y) / direction.Y,
            < 0f => (box.Bottom - origin.Y) / direction.Y,
            _ => float.PositiveInfinity,
        };

        return MathF.Min(tx, ty);
    }
}
