using System.Numerics;
using Content.Server.Administration;
using Content.Server._Goobstation.SpaceWhale.AI;
using Content.Server._Goobstation.SpaceWhale.Brain;
using Content.Server._Goobstation.SpaceWhale.SpaceWhaleSegment;
using Content.Server._Goobstation.SpaceWhale.SpawnLogic;
using Content.Server._Goobstation.SpaceWhale.Threat;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.FixedPoint;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Server._Goobstation.SpaceWhale.Admin;

public abstract partial class SpaceWhaleCommandBase : IConsoleCommand
{
    [Dependency] protected IEntityManager EntManager = default!;
    [Dependency] protected IPlayerManager PlayerManager = default!;
    [Dependency] protected IConfigurationManager Config = default!;
    [Dependency] protected IGameTiming Timing = default!;

    public abstract string Command { get; }
    public virtual string Description => "Админская команда космического кита.";
    public virtual string Help => Command;

    protected WhaleThreatSystem Threat => EntManager.System<WhaleThreatSystem>();
    protected SpaceWhaleSpawnSystem SpawnSystem => EntManager.System<SpaceWhaleSpawnSystem>();
    protected WhaleAbilitySystem Ability => EntManager.System<WhaleAbilitySystem>();
    protected WhaleBrainSystem Brain => EntManager.System<WhaleBrainSystem>();

    protected EntityUid? Whale => TryGetWhale(out var whale) ? whale : null;

    protected bool TryGetWhale(out EntityUid whale)
    {
        whale = default;

        if (Threat.State.CurrentWhale is { } current && IsLiveWhaleHead(current))
        {
            whale = current;
            return true;
        }

        if (TryFindWhaleHead(out whale))
        {
            Threat.SetCurrentWhale(whale);
            return true;
        }

        Threat.SetCurrentWhale(null);
        return false;
    }

    protected bool TryFindWhaleHead(out EntityUid whale)
    {
        whale = default;

        var query = EntManager.AllEntityQueryEnumerator<WhaleBrainComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            if (!IsLiveEntity(uid))
                continue;

            whale = uid;
            return true;
        }

        return false;
    }

    protected List<EntityUid> GetWhaleHeads()
    {
        var whales = new List<EntityUid>();
        var query = EntManager.AllEntityQueryEnumerator<WhaleBrainComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            if (IsLiveEntity(uid))
                AddUnique(whales, uid);
        }

        return whales;
    }

    protected List<EntityUid> GetWhaleSegments()
    {
        var segments = new List<EntityUid>();
        var query = EntManager.AllEntityQueryEnumerator<SpaceWhaleSegmentComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            if (IsLiveEntity(uid))
                AddUnique(segments, uid);
        }

        return segments;
    }

    protected (int Heads, int Segments) QueueDeleteAllWhales()
    {
        var heads = GetWhaleHeads();
        var segments = GetWhaleSegments();

        foreach (var whale in heads)
            EntManager.QueueDeleteEntity(whale);

        foreach (var segment in segments)
            EntManager.QueueDeleteEntity(segment);

        Threat.SetCurrentWhale(null);
        return (heads.Count, segments.Count);
    }

    protected bool IsLiveWhaleHead(EntityUid uid)
    {
        return IsLiveEntity(uid) && EntManager.HasComponent<WhaleBrainComponent>(uid);
    }

    protected bool IsLiveEntity(EntityUid uid)
    {
        return EntManager.EntityExists(uid)
            && EntManager.TryGetComponent<MetaDataComponent>(uid, out var meta)
            && meta.EntityLifeStage < EntityLifeStage.Terminating;
    }

    private static void AddUnique(List<EntityUid> entities, EntityUid uid)
    {
        if (!entities.Contains(uid))
            entities.Add(uid);
    }

    protected bool TryGetCoordinates(IConsoleShell shell, string[] args, int start, out EntityCoordinates coords)
    {
        coords = default;
        var count = args.Length - start;

        if (count == 0)
        {
            if (shell.Player?.AttachedEntity is not { } attached)
                return false;

            coords = EntManager.GetComponent<TransformComponent>(attached).Coordinates;
            return true;
        }

        if (count != 2 ||
            !float.TryParse(args[start], out var x) ||
            !float.TryParse(args[start + 1], out var y) ||
            shell.Player?.AttachedEntity is not { } ent)
        {
            return false;
        }

        var transform = EntManager.System<SharedTransformSystem>();
        var map = transform.GetMapCoordinates(ent);
        coords = new EntityCoordinates(
            EntManager.System<SharedMapSystem>().GetMapOrInvalid(map.MapId),
            new Vector2(x, y));
        return true;
    }

    protected string FormatCoordinates(EntityCoordinates? coords)
    {
        if (coords is not { } value || !value.IsValid(EntManager))
            return "нет";

        var map = EntManager.System<SharedTransformSystem>().ToMapCoordinates(value);
        return $"{map.MapId}:{map.Position.X:0.#},{map.Position.Y:0.#}";
    }

    protected static string FormatBool(bool value)
    {
        return value ? "да" : "нет";
    }

    protected string FormatEntity(EntityUid? entity)
    {
        return entity is { } uid && EntManager.EntityExists(uid)
            ? EntManager.ToPrettyString(uid)
            : "нет";
    }

    protected static string FormatBehavior(WhaleBehavior behavior)
    {
        return behavior switch
        {
            WhaleBehavior.Idle => "покой",
            WhaleBehavior.HuntMob => "охота на живую цель",
            WhaleBehavior.ConsumeTarget => "идёт проглотить цель",
            WhaleBehavior.AttackEntity => "атака неживой угрозы",
            WhaleBehavior.AttackMovingGrid => "атака движущегося грида",
            WhaleBehavior.ExitBreach => "выход через пробой",
            WhaleBehavior.InvestigateNoise => "проверка шума",
            WhaleBehavior.FollowDeathScent => "идёт по запаху смерти",
            WhaleBehavior.ForcedMapHunt => "жёсткий поиск моба по карте",
            WhaleBehavior.Lurk => "медленный обход последней активности",
            _ => behavior.ToString(),
        };
    }

    protected static string FormatPickReason(string reason)
    {
        return reason switch
        {
            "init" => "инициализация",
            "admin-clear" => "админ сбросил память",
            "hunt-mob" => "увидел живую цель",
            "consume-target" => "увидел то, что можно проглотить",
            "attack-entity" => "увидел турель или ядро ИИ",
            "moving-grid" => "нашёл движущийся грид с живыми",
            "exit-breach" => "пытается выйти через пробой",
            "noise" => "услышал шум",
            "death-scent" => "нашёл запах смерти",
            "forced-map-hunt" => "5 минут без убийств, выбран ближайший моб",
            "forced-map-hunt-release" => "жёсткий поиск завершён, моб ближе 100 тайлов",
            "forced-map-hunt-no-target" => "жёсткий поиск не нашёл мобов",
            "lurk" => "обходит последнюю активность",
            "idle" => "нет цели",
            _ => reason,
        };
    }

    protected void ClearBrainNavigation(EntityUid whale, bool clearDeathScents = false)
    {
        if (!EntManager.TryGetComponent<WhaleBrainComponent>(whale, out var brain))
            return;

        brain.CurrentTarget = null;
        brain.CurrentBehavior = WhaleBehavior.Idle;
        brain.LastPickReason = "admin-clear";
        brain.ForcedHuntTarget = null;
        brain.LastActivityCoords = null;
        brain.LastBreachCoords = null;
        brain.InvestigateCoords = null;
        brain.InvestigateUntil = TimeSpan.Zero;
        brain.ActiveNoiseIntensity = 0f;
        brain.LurkCoords = null;
        brain.NextLurkPick = TimeSpan.Zero;
        brain.ActiveDeathScentCoords = null;
        brain.ActiveDeathScentUntil = TimeSpan.Zero;

        if (clearDeathScents)
            brain.DeathScents.Clear();
    }

    public abstract void Execute(IConsoleShell shell, string argStr, string[] args);
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class WhaleDespawnCommand : SpaceWhaleCommandBase
{
    public override string Command => "whaledespawn";
    public override string Description => "Удалить всех космических китов и их сегменты.";
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var deleted = QueueDeleteAllWhales();
        var total = deleted.Heads + deleted.Segments;
        if (total == 0)
        {
            shell.WriteLine("Кит не найден.");
            return;
        }

        shell.WriteLine($"Удаление китов поставлено в очередь: голов {deleted.Heads}, сегментов {deleted.Segments}.");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class WhaleStatusCommand : SpaceWhaleCommandBase
{
    public override string Command => "whalestatus";
    public override string Description => "Показать состояние угрозы, мозга и хвоста кита.";
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var state = Threat.State;
        var threatMax = Config.GetCVar(CCVars.WhaleThreatMax);
        shell.WriteLine("=== Космический кит ===");
        shell.WriteLine($"Пробуждён: {FormatBool(state.IsAwakened)}");
        shell.WriteLine($"Угроза: {state.Threat:0.##} / {threatMax:0.##}");
        shell.WriteLine($"Китов в мире: {GetWhaleHeads().Count}; сегментов: {GetWhaleSegments().Count}");
        shell.WriteLine($"Кит: {FormatEntity(Whale)}");

        if (Whale is not { } wid)
            return;

        if (EntManager.TryGetComponent<WhaleMemoryComponent>(wid, out var memory))
        {
            shell.WriteLine($"Главный раздражитель: {FormatEntity(memory.TopAggressor)}");
            shell.WriteLine($"Записей урона в памяти: {memory.DamageHistory.Count}");
        }

        if (EntManager.TryGetComponent<WhaleBrainComponent>(wid, out var brain))
        {
            var now = Timing.CurTime;

            shell.WriteLine("--- Мозг ---");
            shell.WriteLine($"Режим действия: {FormatBehavior(brain.CurrentBehavior)}");
            shell.WriteLine($"Последнее решение: {FormatPickReason(brain.LastPickReason)}");
            shell.WriteLine($"Интервал тика: {brain.TickInterval:0.##}с  Зрение: {brain.SightRadius:0.#}  Слух: {brain.NoiseInterestRadius:0.#}");
            shell.WriteLine($"Мобы в радиусе зрения: {brain.LastInRangeMobs}; реально видимые: {brain.LastVisibleMobs}");
            shell.WriteLine($"Текущая цель: {FormatEntity(brain.CurrentTarget)}");
            shell.WriteLine($"Жёсткий поиск: цель {FormatEntity(brain.ForcedHuntTarget)}; следующий через {Math.Max(0, (brain.NextForcedHuntAt - now).TotalSeconds):0.#}с; последнее убийство {(brain.LastKillAt == TimeSpan.Zero ? "нет" : $"{Math.Max(0, (now - brain.LastKillAt).TotalSeconds):0.#}с назад")}");
            shell.WriteLine($"Запахов смерти в памяти: {brain.DeathScents.Count}");
            shell.WriteLine($"Последняя активность: {FormatCoordinates(brain.LastActivityCoords)}");
            shell.WriteLine($"Выход через пробой: {FormatCoordinates(brain.LastBreachCoords)}");
            shell.WriteLine($"Шум: {FormatCoordinates(brain.InvestigateCoords)}; сила {brain.ActiveNoiseIntensity:0.#}; ещё {Math.Max(0, (brain.InvestigateUntil - now).TotalSeconds):0.#}с");
            shell.WriteLine($"Активный запах смерти: {FormatCoordinates(brain.ActiveDeathScentCoords)} ещё {Math.Max(0, (brain.ActiveDeathScentUntil - now).TotalSeconds):0.#}с");
            shell.WriteLine($"Точка обхода: {FormatCoordinates(brain.LurkCoords)}");
            if (brain.CurrentTarget is { } targetUid
                && EntManager.TryGetComponent<TransformComponent>(wid, out var whaleXform)
                && EntManager.TryGetComponent<TransformComponent>(targetUid, out var targetXform)
                && whaleXform.Coordinates.TryDistance(EntManager, targetXform.Coordinates, out var dist))
            {
                shell.WriteLine($"Дистанция до цели: {dist:0.##} тайла");
            }

            shell.WriteLine("--- Скорость ---");
            shell.WriteLine($"Сейчас: {brain.CurrentSpeed:0.##}; целевая режима: {brain.LastDesiredSpeed:0.##}; разгон {brain.SpeedAccel:0.#}/с, торможение {brain.SpeedBrakeAccel:0.#}/с");
            shell.WriteLine($"Профили: лурк {brain.LurkSpeed:0.#}, шум {brain.InvestigateSpeed:0.#}/{brain.AlertNoiseSpeed:0.#} при силе >= {brain.AlertNoiseIntensity:0.#}, запах {brain.DeathScentSpeed:0.#}, выход {brain.ExitBreachSpeed:0.#}, охота {brain.HuntingSpeed:0.#}");
        }

        if (EntManager.TryGetComponent<TailedEntityComponent>(wid, out var tail))
        {
            shell.WriteLine("--- Хвост ---");
            shell.WriteLine($"Сегменты: {tail.TailSegments.Count}/{tail.Amount}; множитель скорости головы: {tail.HeadSpeedMultiplier:0.##}");
            shell.WriteLine($"Движение от мозга: {FormatBool(tail.BrainDesiresMovement)}; быстрый режим хвоста: {FormatBool(tail.IsHunting)}");
        }

        shell.WriteLine($"Недавние шумы: {state.RecentNoises.Count}");
        foreach (var noise in state.RecentNoises)
            shell.WriteLine($"  сила={noise.Intensity:0.#}; точка={FormatCoordinates(noise.Coords)}; возраст={(Timing.CurTime - noise.LastUpdatedAt).TotalSeconds:0.#}с");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class WhaleThreatCommand : SpaceWhaleCommandBase
{
    public override string Command => "whalethreat";
    public override string Description => "Выставить уровень угрозы кита.";
    public override string Help => "whalethreat <значение>";
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1 || !float.TryParse(args[0], out var value))
        {
            shell.WriteLine(Help);
            return;
        }

        Threat.SetThreat(value);
        shell.WriteLine($"Угроза кита выставлена: {Threat.State.Threat:0.##}");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class WhaleAwakenCommand : SpaceWhaleCommandBase
{
    public override string Command => "whaleawaken";
    public override string Description => "Пробудить и создать кита около администратора.";
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var coords = shell.Player?.AttachedEntity is { } ent
            ? EntManager.GetComponent<TransformComponent>(ent).Coordinates
            : (EntityCoordinates?) null;

        Threat.Awaken("админская команда");

        if (TryGetWhale(out var existing))
        {
            shell.WriteLine($"Кит уже есть: {FormatEntity(existing)}.");
            return;
        }

        if (!SpawnSystem.TrySpawn(coords, true))
        {
            shell.WriteLine("Кит пробуждён, но создать его не удалось.");
            return;
        }

        shell.WriteLine(TryGetWhale(out var whale)
            ? $"Кит пробуждён и создан: {FormatEntity(whale)}."
            : "Кит пробуждён и создан.");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class WhaleCalmCommand : SpaceWhaleCommandBase
{
    public override string Command => "whalecalm";
    public override string Description => "Сбросить угрозу и удалить всех китов.";
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var deleted = QueueDeleteAllWhales();
        Threat.ResetAll("админская команда");
        shell.WriteLine(deleted.Heads + deleted.Segments == 0
            ? "Состояние кита сброшено."
            : $"Состояние кита сброшено, удаление поставлено в очередь: голов {deleted.Heads}, сегментов {deleted.Segments}.");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class WhaleSpawnCommand : SpaceWhaleCommandBase
{
    public override string Command => "whalespawn";
    public override string Description => "Создать кита у администратора или в координатах текущей карты.";
    public override string Help => "whalespawn [x y]";
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (TryGetWhale(out var existing))
        {
            shell.WriteLine($"Кит уже существует: {FormatEntity(existing)}. Для пересоздания используйте whaledespawn.");
            return;
        }

        EntityCoordinates? coords = null;
        if (args.Length != 0 || shell.Player?.AttachedEntity is { })
        {
            if (!TryGetCoordinates(shell, args, 0, out var parsed))
            {
                shell.WriteLine(Help);
                return;
            }

            coords = parsed;
        }

        if (!SpawnSystem.TrySpawn(coords, true))
            shell.WriteLine("Не удалось создать кита.");
        else
            shell.WriteLine(TryGetWhale(out var whale)
                ? $"Кит создан: {FormatEntity(whale)}."
                : "Кит создан.");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class WhaleGotoCommand : SpaceWhaleCommandBase
{
    public override string Command => "whalegoto";
    public override string Description => "Телепортироваться к текущему киту.";
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (Whale is not { } whale || shell.Player?.AttachedEntity is not { } ent)
        {
            shell.WriteLine("Кит или тело администратора не найдены.");
            return;
        }

        var transform = EntManager.System<SharedTransformSystem>();
        transform.SetCoordinates(ent, EntManager.GetComponent<TransformComponent>(whale).Coordinates);
        transform.AttachToGridOrMap(ent);
        shell.WriteLine("Вы телепортированы к киту.");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class WhaleBringCommand : SpaceWhaleCommandBase
{
    public override string Command => "whalebring";
    public override string Description => "Переместить текущего кита к администратору.";
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (Whale is not { } whale || shell.Player?.AttachedEntity is not { } ent)
        {
            shell.WriteLine("Кит или тело администратора не найдены.");
            return;
        }

        var transform = EntManager.System<SharedTransformSystem>();
        transform.SetCoordinates(whale, EntManager.GetComponent<TransformComponent>(ent).Coordinates);
        transform.AttachToGridOrMap(whale);

        ClearBrainNavigation(whale);
        if (EntManager.TryGetComponent<TransformComponent>(whale, out var whaleXform))
            Brain.RememberActivity(whale, whaleXform.Coordinates);

        shell.WriteLine("Кит перемещён к администратору, навигационная память сброшена.");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class WhaleRoarCommand : SpaceWhaleCommandBase
{
    public override string Command => "whaleroar";
    public override string Description => "Заставить текущего кита издать рёв.";
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (Whale is not { } whale)
        {
            shell.WriteLine("Кит не найден.");
            return;
        }

        Ability.TryRoar(whale, 0f);
        shell.WriteLine("Кит издал рёв.");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class WhaleClearMemoryCommand : SpaceWhaleCommandBase
{
    public override string Command => "whaleclearmemory";
    public override string Description => "Очистить агрессию, навигацию и запахи смерти текущего кита.";
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (Whale is not { } whale)
        {
            shell.WriteLine("Кит не найден.");
            return;
        }

        if (EntManager.TryGetComponent<WhaleMemoryComponent>(whale, out var memory))
        {
            memory.TopAggressor = null;
            memory.DamageHistory.Clear();
        }

        ClearBrainNavigation(whale, true);

        shell.WriteLine("Агрессия, навигация, пробой, шум и запахи смерти кита очищены.");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class WhaleClearNoiseCommand : SpaceWhaleCommandBase
{
    public override string Command => "whaleclearnoise";
    public override string Description => "Очистить сохранённые точки шума кита.";
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var count = Threat.State.RecentNoises.Count;
        Threat.State.RecentNoises.Clear();
        shell.WriteLine($"Очищено точек шума: {count}.");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class WhaleNoiseCommand : SpaceWhaleCommandBase
{
    public override string Command => "whalenoise";
    public override string Description => "Добавить точку шума для реакции кита.";
    public override string Help => "whalenoise [сила] [x y]";
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var intensity = 100f;
        var coordStart = 0;

        if (args.Length is 1 or 3)
        {
            if (!float.TryParse(args[0], out intensity))
            {
                shell.WriteLine(Help);
                return;
            }

            coordStart = 1;
        }

        if (!TryGetCoordinates(shell, args, coordStart, out var coords))
        {
            shell.WriteLine(Help);
            return;
        }

        Threat.AddNoise(coords, intensity);
        shell.WriteLine($"Добавлен шум для кита: сила {intensity:0.#}, точка {FormatCoordinates(coords)}.");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class WhaleScentCommand : SpaceWhaleCommandBase
{
    public override string Command => "whalescent";
    public override string Description => "Добавить точку запаха смерти для текущего кита.";
    public override string Help => "whalescent [x y]";
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (Whale is not { } whale || !TryGetCoordinates(shell, args, 0, out var coords))
        {
            shell.WriteLine(Help);
            return;
        }

        Brain.RememberDeathScent(whale, coords);
        shell.WriteLine($"Добавлен запах смерти в точке {FormatCoordinates(coords)}.");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class WhaleBreachCommand : SpaceWhaleCommandBase
{
    public override string Command => "whalebreach";
    public override string Description => "Задать точку выхода через пробой для текущего кита.";
    public override string Help => "whalebreach [x y]";
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (Whale is not { } whale || !TryGetCoordinates(shell, args, 0, out var coords))
        {
            shell.WriteLine(Help);
            return;
        }

        Brain.RememberBreach(whale, coords);
        shell.WriteLine($"Точка выхода через пробой задана: {FormatCoordinates(coords)}.");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class WhaleAggroCommand : SpaceWhaleCommandBase
{
    public override string Command => "whaleaggro";
    public override string Description => "Назначить главного раздражителя текущего кита.";
    public override string Help => "whaleaggro <игрок|netEntity>";
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (Whale is not { } whale || args.Length != 1 || !TryResolveTarget(args[0], out var target))
        {
            shell.WriteLine(Help);
            return;
        }

        var memory = EntManager.EnsureComponent<WhaleMemoryComponent>(whale);
        memory.TopAggressor = target;
        memory.DamageHistory[target] =
        [
            new WhaleDamageRecord { Time = Timing.CurTime, Amount = FixedPoint2.New(1) },
        ];

        shell.WriteLine($"Главный раздражитель назначен: {EntManager.ToPrettyString(target)}");
    }

    private bool TryResolveTarget(string value, out EntityUid target)
    {
        if (NetEntity.TryParse(value, out var netEntity)
            && EntManager.TryGetEntity(netEntity, out var parsedTarget)
            && parsedTarget is { } parsed)
        {
            target = parsed;
            return true;
        }

        if (PlayerManager.TryGetSessionByUsername(value, out var session) && session.AttachedEntity is { } attached)
        {
            target = attached;
            return true;
        }

        target = default;
        return false;
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class WhaleDebugCommand : SpaceWhaleCommandBase
{
    public override string Command => "whaledebug";
    public override string Description => "Включить или выключить сообщения кита в админ-чат.";
    public override string Help => "whaledebug <on|off|вкл|выкл>";
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1 || !TryParseToggle(args[0], out var enabled))
        {
            shell.WriteLine(Help);
            return;
        }

        Config.SetCVar(CCVars.WhaleAdminChatSpam, enabled);
        shell.WriteLine(enabled
            ? "Сообщения кита в админ-чат включены."
            : "Сообщения кита в админ-чат выключены.");
    }

    private static bool TryParseToggle(string value, out bool enabled)
    {
        switch (value.ToLowerInvariant())
        {
            case "on":
            case "true":
            case "1":
            case "yes":
            case "да":
            case "вкл":
                enabled = true;
                return true;
            case "off":
            case "false":
            case "0":
            case "no":
            case "нет":
            case "выкл":
                enabled = false;
                return true;
            default:
                enabled = false;
                return false;
        }
    }
}
