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
}

[RegisterComponent]
public sealed partial class WhaleConsumerComponent : Component
{
    [DataField] public float SearchRadius = 2f;
    [ViewVariables] public TimeSpan NextScan;
    [DataField] public float ScanInterval = 1f;
}

/// <summary>
/// The whale's "brain" — tracks the current target and tick timer.
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

    /// <summary>
    /// How far from the station's outer edge the whale should orbit.
    /// </summary>
    [DataField] public float OrbitClearance = 10f;

    // ----- Скорость с плавным разгоном/торможением -----

    /// <summary>
    /// Минимальная (крейсерская) скорость — когда нет цели-моба.
    /// </summary>
    [DataField] public float CruiseSpeed = 7f;

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
    /// Текущая эффективная скорость (плавно меняется).
    /// </summary>
    [ViewVariables] public float CurrentSpeed = 7f;

    [ViewVariables] public EntityUid? CurrentTarget;

    /// <summary>
    /// Debug — что решил кит на последнем тике: "mob" / "orbit" / "idle".
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

}
