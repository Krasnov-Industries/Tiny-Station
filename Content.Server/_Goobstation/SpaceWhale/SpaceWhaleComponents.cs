using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Map;

namespace Content.Server._Goobstation.SpaceWhale;

[RegisterComponent]
public sealed partial class WhaleSpawnedByComponent : Component;


[RegisterComponent]
public sealed partial class SpaceWhaleSegmentComponent : Component
{
    [ViewVariables] public EntityUid? Whale;
    [ViewVariables] public int Index;

    /// <summary>
    /// Сегмент сейчас активно упирается в предыдущий (зажат, расстояние > spacing × PushDistanceFactor).
    /// Выставляется TailedEntitySystem каждый тик; читается DamageOnCollideSystem.
    /// </summary>
    [ViewVariables] public bool IsPushing;
}

public sealed class WhaleNoiseSnapshot
{
    public EntityCoordinates Coords;
    public float Intensity;
    public TimeSpan FirstHeardAt;
    public TimeSpan LastUpdatedAt;
    public MapId MapId;
}

public sealed class WhaleThreatState
{
    public float Threat;
    public bool IsAwakened;
    [ViewVariables] public TimeSpan AwakenedAt;
    [ViewVariables] public List<WhaleNoiseSnapshot> RecentNoises = new();
    [ViewVariables] public EntityUid? CurrentWhale;
    [ViewVariables] public Dictionary<EntityUid, TimeSpan> FarFromStationSince = new();
    [ViewVariables] public bool WarningAnnounced;
}

[RegisterComponent]
public sealed partial class WhaleAuraComponent : Component
{
    [ViewVariables] public TimeSpan NextTick;
}

[RegisterComponent]
public sealed partial class WhaleAffectedLightComponent : Component
{
    [ViewVariables] public TimeSpan RestoreAt;
}

[RegisterComponent]
public sealed partial class WhaleMemoryComponent : Component
{
    [DataField] public float AggressionWindow = 30f;
    [ViewVariables] public EntityUid? TopAggressor;
    [ViewVariables] public Dictionary<EntityUid, List<WhaleDamageRecord>> DamageHistory = new();
}

public sealed class WhaleDamageRecord
{
    public TimeSpan Time;
    public FixedPoint2 Amount;
}

public enum WhaleBehavior
{
    Idle,
    Lurk,
    InvestigateNoise,
    FollowDeathScent,
    HuntMob,
    ForcedMapHunt,
    ConsumeTarget,
    AttackEntity,
    AttackMovingGrid,
    ExitBreach,
}

public sealed class WhaleDeathScent
{
    public EntityCoordinates Coords;
    public TimeSpan CreatedAt;
}

[RegisterComponent]
public sealed partial class WhaleAbilityComponent : Component
{
    [ViewVariables] public TimeSpan NextRoar;
}

[RegisterComponent]
public sealed partial class DamageOnCollideComponent : Component
{
    /// <summary>
    /// If true — damage goes to the other entity (target).
    /// If false — damage goes to us. For the whale we set this true.
    /// </summary>
    [DataField] public bool Inverted;

    [DataField(required: true)] public DamageSpecifier Damage = new();

    /// <summary>
    /// Минимальный интервал (сек) между ударами по одной и той же цели.
    /// 0 — без cooldown, бьём на каждый StartCollide.
    /// </summary>
    [DataField] public float Cooldown;

    /// <summary>
    /// Extra range for damaging nearby damageable non-mobs that do not produce
    /// physics contacts, like wallmounts and signs. 0 disables the sweep.
    /// </summary>
    [DataField] public float NearbyDamageRadius;

    /// <summary>
    /// Если true — урон наносится только когда мы помечены как "толкающиеся"
    /// (для сегментов кита: расстояние до соседа выше порога).
    /// </summary>
    [DataField] public bool RequirePushing;

    /// <summary>
    /// Runtime: следующее допустимое время удара для конкретной цели.
    /// </summary>
    [ViewVariables] public Dictionary<EntityUid, TimeSpan> NextHit = new();
}

[RegisterComponent]
public sealed partial class WhaleEatenCorpseComponent : Component
{
    [ViewVariables] public TimeSpan EatenAt;
    [ViewVariables] public EntityUid? EatenBy;
    [ViewVariables] public bool PreserveInStomach;
}

[RegisterComponent]
public sealed partial class WhaleConsumerComponent : Component
{
    [DataField] public float SearchRadius = 2f;
    [ViewVariables] public TimeSpan NextScan;
    [DataField] public float ScanInterval = 1f;
}

/// <summary>
/// The whale's "brain" — tracks current behavior and short-lived memory.
/// </summary>
[RegisterComponent]
public sealed partial class WhaleBrainComponent : Component
{
    [DataField] public float TickInterval = 0.3f;

    [ViewVariables] public TimeSpan NextTick;

    /// <summary>
    /// LOS sight radius. Sees through holes/doors/etc.
    /// </summary>
    [DataField] public float SightRadius = 30f;

    [DataField] public float LurkMinRadius = 15f;

    [DataField] public float LurkMaxRadius = 40f;

    [DataField] public float LurkPickInterval = 15f;

    [DataField] public float DeathScentTtl = 300f;

    [DataField] public int MaxDeathScents = 10;

    [DataField] public float InvestigateDuration = 30f;

    [DataField] public float NoiseInterestRadius = 200f;

    [DataField] public float InvestigateArrivalRadius = 4f;

    [DataField] public float DeathScentFollowDuration = 20f;

    [DataField] public float DeathScentArrivalRadius = 4f;

    [DataField] public float LurkArrivalRadius = 5f;

    [DataField] public float BreachExitArrivalRadius = 3f;

    [DataField] public float ForcedHuntNoKillDelay = 300f;

    [DataField] public float ForcedHuntReleaseRadius = 35f;

    // ----- Скорость с плавным разгоном/торможением -----

    /// <summary>
    /// Базовая крейсерская скорость для старых/неизвестных режимов.
    /// </summary>
    [DataField] public float CruiseSpeed = 7f;

    /// <summary>
    /// Медленный обход последней активности.
    /// </summary>
    [DataField] public float LurkSpeed = 2.5f;

    /// <summary>
    /// Проверка обычного шума и осторожное движение к точкам интереса.
    /// </summary>
    [DataField] public float InvestigateSpeed = 4f;

    /// <summary>
    /// Скорость движения к резкому/сильному шуму.
    /// </summary>
    [DataField] public float AlertNoiseSpeed = 8f;

    /// <summary>
    /// Сила шума, с которой проверка становится быстрым выходом на точку.
    /// </summary>
    [DataField] public float AlertNoiseIntensity = 50f;

    /// <summary>
    /// Скорость движения по старым точкам смерти.
    /// </summary>
    [DataField] public float DeathScentSpeed = 3.5f;

    /// <summary>
    /// Скорость выхода со станции через запомненный пробой.
    /// </summary>
    [DataField] public float ExitBreachSpeed = 4.5f;

    /// <summary>
    /// Максимальная (погоня) скорость — постепенно нарастает при наличии
    /// живой цели-моба.
    /// </summary>
    [DataField] public float HuntingSpeed = 14f;

    /// <summary>
    /// Изменение скорости (тайл/сек) за секунду — разгон/торможение.
    /// 4 значит "за ~2 секунды переходит с 7 на 14".
    /// </summary>
    [DataField] public float SpeedAccel = 2f;

    /// <summary>
    /// Торможение при переходе из погони в спокойный режим.
    /// </summary>
    [DataField] public float SpeedBrakeAccel = 5f;

    /// <summary>
    /// Текущая эффективная скорость (плавно меняется).
    /// </summary>
    [ViewVariables] public float CurrentSpeed = 7f;

    /// <summary>
    /// Последняя целевая скорость выбранного режима. Для whalestatus.
    /// </summary>
    [ViewVariables] public float LastDesiredSpeed;

    [ViewVariables] public EntityUid? CurrentTarget;

    [ViewVariables] public EntityUid? ForcedHuntTarget;

    [ViewVariables] public TimeSpan LastKillAt;

    [ViewVariables] public TimeSpan NextForcedHuntAt;

    [ViewVariables] public WhaleBehavior CurrentBehavior = WhaleBehavior.Idle;

    /// <summary>
    /// Debug — что решил кит на последнем тике.
    /// </summary>
    [ViewVariables] public string LastPickReason = "init";

    /// <summary>
    /// Debug — сколько живых мобов было в SightRadius на последнем тике (после
    /// фильтра по живости и не-сегменту/не-киту).
    /// </summary>
    [ViewVariables] public int LastInRangeMobs;

    /// <summary>
    /// Debug — сколько из них прошли LOS-проверку (реально видимые).
    /// </summary>
    [ViewVariables] public int LastVisibleMobs;

    [ViewVariables] public EntityCoordinates? LastActivityCoords;

    [ViewVariables] public EntityCoordinates? LastBreachCoords;

    [ViewVariables] public EntityCoordinates? InvestigateCoords;

    [ViewVariables] public TimeSpan InvestigateUntil;

    [ViewVariables] public float ActiveNoiseIntensity;

    [ViewVariables] public EntityCoordinates? LurkCoords;

    [ViewVariables] public TimeSpan NextLurkPick;

    [ViewVariables] public EntityCoordinates? ActiveDeathScentCoords;

    [ViewVariables] public TimeSpan ActiveDeathScentUntil;

    [ViewVariables] public List<WhaleDeathScent> DeathScents = new();
}
