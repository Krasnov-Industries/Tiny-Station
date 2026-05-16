using System.Numerics;
using Content.Server._Goobstation.SpaceWhale.AI;
using Content.Server._Goobstation.SpaceWhale.SpaceWhaleSegment;
using Content.Server._Goobstation.SpaceWhale.Threat;
using Content.Shared.CombatMode;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Physics;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Goobstation.SpaceWhale.Brain;

/// <summary>
/// Простая модель восприятия:
///   1. Ближайший живой моб в SightRadius с прямой видимостью (LOS).
///      TopAggressor (кто бил больше всех за окно памяти) — приоритет.
///   2. Орбита станции — fallback, если никого не видно.
/// Никаких слоёв "свет/темнота/осязание" — кит видит то, что видно через
/// прозрачные пространства/окна. За непрозрачной стеной — не видит.
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

    private const float MeleeRange = 3.5f;
    private const float RoarCooldown = 10f;
    private const float RoarTriggerRadius = 8f;
    private const float OrbitAdvanceRadians = 0.35f;

    /// <summary>
    /// Видимость космического кита: блокируют только непрозрачные объекты
    /// (стены). Окна, решётки, прозрачные двери — пропускают. Кит видит
    /// добычу внутри станции с космоса через иллюминаторы.
    /// </summary>
    private const CollisionGroup SightMask = CollisionGroup.Opaque;

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

        var target = PickTarget(whale, brain, xform);
        brain.CurrentTarget = target.Entity;

        // Флаг "погони" для TailedEntity — гасит wave, делает движение
        // более целеустремлённым при наличии живой цели-моба.
        if (TryComp<Content.Server._Goobstation.SpaceWhale.SpaceWhaleSegment.TailedEntityComponent>(whale, out var tail))
            tail.IsHunting = target.Entity != null;

        // Рывок: если одна и та же цель упорно не приближается N секунд —
        // импульс к ней.
        UpdateChargeTracking(whale, brain, xform, target.Entity, tail);

        MoveTo(whale, xform, target.Coords);

        if (target.Entity is { } victim && TryComp<MobStateComponent>(victim, out var mobState) &&
            _mobState.IsAlive(victim, mobState) && InMeleeRange(xform, victim))
        {
            TryBite(whale, victim);
        }

        if (target.Entity != null || HasAnyLivingNear(whale, xform, RoarTriggerRadius))
            _abilities.TryRoar(whale, RoarCooldown);
    }

    private void UpdateChargeTracking(
        EntityUid whale,
        WhaleBrainComponent brain,
        TransformComponent xform,
        EntityUid? targetEntity,
        Content.Server._Goobstation.SpaceWhale.SpaceWhaleSegment.TailedEntityComponent? tail)
    {
        // Нет цели — сбрасываем накопление.
        if (targetEntity is not { } victim || !Exists(victim))
        {
            brain.LastChargeTarget = null;
            brain.ChargeBuildup = 0f;
            return;
        }

        var whalePos = _transform.GetWorldPosition(xform);
        var targetPos = _transform.GetWorldPosition(Transform(victim));
        var dist = (targetPos - whalePos).Length();

        // Цель сменилась — обнуляем счётчик.
        if (brain.LastChargeTarget != victim)
        {
            brain.LastChargeTarget = victim;
            brain.LastChargeDistance = dist;
            brain.ChargeBuildup = 0f;
            return;
        }

        // Та же цель — приблизился ли существенно?
        if (brain.LastChargeDistance - dist > brain.ChargeProgressEpsilon)
            brain.ChargeBuildup = 0f;
        else
            brain.ChargeBuildup += brain.TickInterval;

        brain.LastChargeDistance = dist;

        // Накопили достаточно и cooldown прошёл — рывок.
        var now = _timing.CurTime;
        if (brain.ChargeBuildup >= brain.ChargeThresholdSeconds && now >= brain.ChargeReadyAt)
        {
            TriggerCharge(whale, brain, whalePos, targetPos, tail);
            brain.ChargeBuildup = 0f;
            brain.ChargeReadyAt = now + TimeSpan.FromSeconds(brain.ChargeCooldownSeconds);
        }
    }

    private void TriggerCharge(
        EntityUid whale,
        WhaleBrainComponent brain,
        Vector2 from,
        Vector2 to,
        Content.Server._Goobstation.SpaceWhale.SpaceWhaleSegment.TailedEntityComponent? tail)
    {
        var dir = to - from;
        var len = dir.Length();
        if (len < 0.1f)
            return;
        dir /= len;

        if (TryComp<PhysicsComponent>(whale, out var phys))
            _physics.SetLinearVelocity(whale, dir * brain.ChargeSpeed, body: phys);

        _transform.SetWorldRotation(whale, dir.ToWorldAngle());

        // Просим TailedEntitySystem не переопределять нашу velocity
        // следующие ChargeDurationSeconds секунд.
        if (tail != null)
            tail.BrainVelocityOverrideUntil = _timing.CurTime + TimeSpan.FromSeconds(brain.ChargeDurationSeconds);
    }

    private readonly record struct PickResult(EntityUid? Entity, EntityCoordinates? Coords);

    private PickResult PickTarget(EntityUid whale, WhaleBrainComponent brain, TransformComponent xform)
    {
        var coords = xform.Coordinates;

        if (TryPickVisibleMob(whale, brain, coords, out var visible, out var stats))
        {
            brain.LastPickReason = "mob";
            brain.LastVisibleMobs = stats.Visible;
            brain.LastInRangeMobs = stats.InRange;
            return new PickResult(visible, Transform(visible).Coordinates);
        }

        // Движущийся грид (шатл / спасательная капсула / прочая жизнь в космосе).
        // Глухой шатл без окон не пропускает свет, но движение его массы кит
        // ощущает — есть кого там пощупать.
        if (TryPickMovingGrid(whale, brain, xform, out var gridCoords))
        {
            brain.LastPickReason = "shuttle";
            brain.LastVisibleMobs = stats.Visible;
            brain.LastInRangeMobs = stats.InRange;
            return new PickResult(null, gridCoords);
        }

        brain.LastPickReason = "orbit";
        brain.LastVisibleMobs = stats.Visible;
        brain.LastInRangeMobs = stats.InRange;

        if (_threat.TryGetNearestStationOrbitPoint(coords, brain.OrbitClearance, OrbitAdvanceRadians, out var orbit))
            return new PickResult(null, orbit);

        brain.LastPickReason = "idle";
        return new PickResult(null, null);
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
            if (target == whale)
                continue;
            if (HasComp<WhaleSpawnedByComponent>(target) || HasComp<SpaceWhaleSegmentComponent>(target))
                continue;
            if (!_mobState.IsAlive(target, cand.Comp))
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

            if (!TryComp<PhysicsComponent>(grid.Owner, out var phys))
                continue;
            // Меньше 0.5 тайла/с = "стоит". Не цель.
            if (phys.LinearVelocity.LengthSquared() < 0.25f)
                continue;

            // Грид без живых мобов — обломок/мусор/спам-капсула. Не интересно.
            if (!HasLivingMobsOnGrid(grid.Owner))
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

    private bool HasLivingMobsOnGrid(EntityUid gridUid)
    {
        var enumerator = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (enumerator.MoveNext(out var uid, out var mob, out var xf))
        {
            if (xf.GridUid != gridUid)
                continue;
            if (HasComp<WhaleSpawnedByComponent>(uid) || HasComp<SpaceWhaleSegmentComponent>(uid))
                continue;
            if (!_mobState.IsAlive(uid, mob))
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

    private bool InMeleeRange(TransformComponent whaleXform, EntityUid target)
    {
        return whaleXform.Coordinates.TryDistance(EntityManager, Transform(target).Coordinates, out var dist) &&
               dist <= MeleeRange;
    }

    private bool HasAnyLivingNear(EntityUid whale, TransformComponent xform, float radius)
    {
        foreach (var candidate in _lookup.GetEntitiesInRange<MobStateComponent>(xform.Coordinates, radius))
        {
            if (candidate.Owner == whale ||
                HasComp<WhaleSpawnedByComponent>(candidate.Owner) ||
                HasComp<SpaceWhaleSegmentComponent>(candidate.Owner))
                continue;

            if (_mobState.IsAlive(candidate.Owner, candidate.Comp))
                return true;
        }

        return false;
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
        var speed = TryComp<MovementSpeedModifierComponent>(whale, out var modifier)
            ? modifier.CurrentSprintSpeed
            : 5f;

        var tail = CompOrNull<Content.Server._Goobstation.SpaceWhale.SpaceWhaleSegment.TailedEntityComponent>(whale);
        if (tail != null)
            speed *= Math.Clamp(tail.HeadSpeedMultiplier, 0f, 1f);

        if (TryComp<PhysicsComponent>(whale, out var physics))
            _physics.SetLinearVelocity(whale, speed <= 0.05f ? Vector2.Zero : direction * speed, body: physics);

        _transform.SetWorldRotation(whale, direction.ToWorldAngle());

        if (tail != null)
            tail.BrainDesiresMovement = speed > 0.05f;
    }

    private void Stop(EntityUid whale)
    {
        if (TryComp<PhysicsComponent>(whale, out var physics))
            _physics.SetLinearVelocity(whale, Vector2.Zero, body: physics);
        if (TryComp<Content.Server._Goobstation.SpaceWhale.SpaceWhaleSegment.TailedEntityComponent>(whale, out var tail))
            tail.BrainDesiresMovement = false;
    }

    private T? CompOrNull<T>(EntityUid uid) where T : IComponent
        => TryComp<T>(uid, out var comp) ? comp : default;

    private void TryBite(EntityUid whale, EntityUid target)
    {
        if (!TryComp<MeleeWeaponComponent>(whale, out var weapon))
            return;

        if (weapon.NextAttack > _timing.CurTime)
            return;

        _melee.AttemptLightAttack(whale, whale, weapon, target);
    }
}
