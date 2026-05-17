using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Map;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Goobstation.SpaceWhale;

[RegisterComponent]
public sealed partial class WhaleSpawnedByComponent : Component;


[RegisterComponent]
public sealed partial class SpaceWhaleSegmentComponent : Component
{
    [DataField] public EntityUid? Whale;
    [DataField] public int Index;

    /// <summary>
    /// Сегмент сейчас активно упирается в предыдущий (зажат, расстояние > spacing × PushDistanceFactor).
    /// Выставляется TailedEntitySystem каждый тик; читается DamageOnCollideSystem.
    /// </summary>
    [ViewVariables] public bool IsPushing;
}

[RegisterComponent]
public sealed partial class SpaceWhaleSpawnerComponent : Component;

public sealed class WhaleNoiseSnapshot
{
    public EntityCoordinates Coords;
    public float Intensity;
    public TimeSpan FirstHeardAt;
    public TimeSpan LastUpdatedAt;
    public MapId MapId;
}

[RegisterComponent]
public sealed partial class WhaleThreatComponent : Component
{
    [DataField] public float Threat;
    [DataField] public bool IsAwakened;
    [ViewVariables] public List<WhaleNoiseSnapshot> RecentNoises = new();
    [DataField] public EntityUid? CurrentWhale;
    [DataField] public Dictionary<EntityUid, TimeSpan> FarFromStationSince = new();
    [DataField] public bool WarningAnnounced;
}

[RegisterComponent]
public sealed partial class WhaleAuraComponent : Component
{
    [DataField] public float Radius = 6f;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextTick;
}

[RegisterComponent]
public sealed partial class WhaleAffectedLightComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan RestoreAt;
}

[RegisterComponent]
public sealed partial class WhaleMemoryComponent : Component
{
    [DataField] public float AggressionWindow = 30f;
    [ViewVariables] public EntityUid? TopAggressor;
    [ViewVariables] public Dictionary<EntityUid, List<WhaleDamageRecord>> DamageHistory = new();
}

[DataDefinition]
public sealed partial class WhaleDamageRecord
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan Time;
    [DataField] public FixedPoint2 Amount;
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
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan EatenAt;
}

[RegisterComponent]
public sealed partial class WhaleConsumerComponent : Component
{
    [DataField] public float SearchRadius = 2f;
    [DataField] public float HealPerCorpse = 500f;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextScan;
    [DataField] public float ScanInterval = 1f;
}

/// <summary>
/// The whale's "brain" — tracks the current target and tick timer.
/// </summary>
[RegisterComponent]
public sealed partial class WhaleBrainComponent : Component
{
    [DataField] public float TickInterval = 0.3f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextTick;

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
