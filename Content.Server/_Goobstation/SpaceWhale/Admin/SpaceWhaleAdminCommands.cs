using Content.Server.Administration;
using Content.Server._Goobstation.SpaceWhale.AI;
using Content.Server._Goobstation.SpaceWhale.SpaceWhaleSegment;
using Content.Server._Goobstation.SpaceWhale.SpawnLogic;
using Content.Server._Goobstation.SpaceWhale.Threat;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
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
    public virtual string Description => "Space whale admin command.";
    public virtual string Help => Command;

    protected WhaleThreatSystem Threat => EntManager.System<WhaleThreatSystem>();
    protected SpaceWhaleSpawnSystem SpawnSystem => EntManager.System<SpaceWhaleSpawnSystem>();
    protected WhaleAbilitySystem Ability => EntManager.System<WhaleAbilitySystem>();

    protected EntityUid? Whale => Threat.State.CurrentWhale != null && EntManager.EntityExists(Threat.State.CurrentWhale.Value)
        ? Threat.State.CurrentWhale.Value
        : null;

    public abstract void Execute(IConsoleShell shell, string argStr, string[] args);
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class WhaleDespawnCommand : SpaceWhaleCommandBase
{
    public override string Command => "whaledespawn";
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (Whale is not { } whale)
        {
            shell.WriteLine("No whale.");
            return;
        }

        EntManager.QueueDeleteEntity(whale);
        shell.WriteLine("Whale despawn queued.");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class WhaleStatusCommand : SpaceWhaleCommandBase
{
    public override string Command => "whalestatus";
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var state = Threat.State;
        var threatMax = Config.GetCVar(CCVars.WhaleThreatMax);
        shell.WriteLine("=== Space Whale Status ===");
        shell.WriteLine($"Awakened: {state.IsAwakened}");
        shell.WriteLine($"Threat: {state.Threat:0.##} / {threatMax:0.##}");
        shell.WriteLine($"Whale: {(Whale is { } whale ? EntManager.ToPrettyString(whale) : "none")}");

        if (Whale is not { } wid)
            return;

        if (EntManager.TryGetComponent<WhaleMemoryComponent>(wid, out var memory))
        {
            shell.WriteLine($"Top aggressor: {(memory.TopAggressor is { } aggro ? EntManager.ToPrettyString(aggro) : "none")}");
            shell.WriteLine($"DamageHistory entries: {memory.DamageHistory.Count}");
        }

        if (EntManager.TryGetComponent<WhaleBrainComponent>(wid, out var brain))
        {
            var now = Timing.CurTime;

            shell.WriteLine($"--- Brain ---");
            shell.WriteLine($"Tick interval: {brain.TickInterval}s  Sight radius: {brain.SightRadius}");
            shell.WriteLine($"Last decision: {brain.LastPickReason}");
            shell.WriteLine($"Mobs in SightRadius (filtered alive/not-self): {brain.LastInRangeMobs}");
            shell.WriteLine($"Of them passed LOS:                            {brain.LastVisibleMobs}");
            shell.WriteLine($"Current target: {(brain.CurrentTarget is { } tgt ? EntManager.ToPrettyString(tgt) : "none")}");
            if (brain.CurrentTarget is { } targetUid
                && EntManager.TryGetComponent<TransformComponent>(wid, out var whaleXform)
                && EntManager.TryGetComponent<TransformComponent>(targetUid, out var targetXform)
                && whaleXform.Coordinates.TryDistance(EntManager, targetXform.Coordinates, out var dist))
            {
                shell.WriteLine($"Distance to target: {dist:0.##} tiles");
            }

            shell.WriteLine($"--- Speed ---");
            shell.WriteLine($"Current: {brain.CurrentSpeed:0.##} (cruise {brain.CruiseSpeed:0.#} → hunting {brain.HuntingSpeed:0.#}, accel {brain.SpeedAccel:0.#}/s)");
        }

        if (EntManager.TryGetComponent<TailedEntityComponent>(wid, out var tail))
        {
            shell.WriteLine($"--- Tail ---");
            shell.WriteLine($"Segments: {tail.TailSegments.Count}/{tail.Amount}  HeadSpeedMultiplier: {tail.HeadSpeedMultiplier:0.##}");
        }

        shell.WriteLine($"Recent noises: {state.RecentNoises.Count}");
        foreach (var noise in state.RecentNoises)
            shell.WriteLine($"  intensity={noise.Intensity:0.#} at {noise.Coords} (updated {noise.LastUpdatedAt})");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class WhaleThreatCommand : SpaceWhaleCommandBase
{
    public override string Command => "whalethreat";
    public override string Help => "whalethreat <value>";
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1 || !float.TryParse(args[0], out var value))
        {
            shell.WriteLine(Help);
            return;
        }

        Threat.SetThreat(value);
        shell.WriteLine($"Threat set to {Threat.State.Threat:0.##}");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class WhaleAwakenCommand : SpaceWhaleCommandBase
{
    public override string Command => "whaleawaken";
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        Threat.Awaken("admin command", shell.Player?.AttachedEntity is { } ent ? EntManager.GetComponent<TransformComponent>(ent).Coordinates : null);
        shell.WriteLine("Whale awakened.");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class WhaleCalmCommand : SpaceWhaleCommandBase
{
    public override string Command => "whalecalm";
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        Threat.ResetAll("admin command");
        shell.WriteLine("Whale state reset.");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class WhaleSpawnCommand : SpaceWhaleCommandBase
{
    public override string Command => "whalespawn";
    public override string Help => "whalespawn [x y]";
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        EntityCoordinates? coords = null;
        if (args.Length == 2 &&
            float.TryParse(args[0], out var x) &&
            float.TryParse(args[1], out var y) &&
            shell.Player?.AttachedEntity is { } attached)
        {
            var map = EntManager.System<SharedTransformSystem>().GetMapCoordinates(attached);
            coords = new EntityCoordinates(EntManager.System<SharedMapSystem>().GetMapOrInvalid(map.MapId), new System.Numerics.Vector2(x, y));
        }
        else if (shell.Player?.AttachedEntity is { } ent)
        {
            coords = EntManager.GetComponent<TransformComponent>(ent).Coordinates;
        }

        if (!SpawnSystem.TrySpawn(coords))
            shell.WriteLine("Failed to spawn whale.");
        else
            shell.WriteLine("Whale spawned.");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class WhaleKillCommand : SpaceWhaleCommandBase
{
    public override string Command => "whalekill";
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (Whale is not { } whale)
        {
            shell.WriteLine("No whale.");
            return;
        }

        EntManager.System<DamageableSystem>().TryChangeDamage(whale, new DamageSpecifier { DamageDict = { ["Blunt"] = FixedPoint2.New(100000) } }, true);
        shell.WriteLine("Whale killed.");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class WhaleGotoCommand : SpaceWhaleCommandBase
{
    public override string Command => "whalegoto";
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (Whale is not { } whale || shell.Player?.AttachedEntity is not { } ent)
            return;

        var transform = EntManager.System<SharedTransformSystem>();
        transform.SetCoordinates(ent, EntManager.GetComponent<TransformComponent>(whale).Coordinates);
        transform.AttachToGridOrMap(ent);
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class WhaleBringCommand : SpaceWhaleCommandBase
{
    public override string Command => "whalebring";
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (Whale is not { } whale || shell.Player?.AttachedEntity is not { } ent)
            return;

        var transform = EntManager.System<SharedTransformSystem>();
        transform.SetCoordinates(whale, EntManager.GetComponent<TransformComponent>(ent).Coordinates);
        transform.AttachToGridOrMap(whale);
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class WhaleRoarCommand : SpaceWhaleCommandBase
{
    public override string Command => "whaleroar";
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (Whale is { } whale) Ability.TryRoar(whale, 0f);
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class WhaleClearMemoryCommand : SpaceWhaleCommandBase
{
    public override string Command => "whaleclearmemory";
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (Whale is { } whale && EntManager.TryGetComponent<WhaleMemoryComponent>(whale, out var memory))
        {
            memory.TopAggressor = null;
            memory.DamageHistory.Clear();
        }
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class WhaleAggroCommand : SpaceWhaleCommandBase
{
    public override string Command => "whaleaggro";
    public override string Help => "whaleaggro <player|netEntity>";
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

        shell.WriteLine($"Top aggressor set to {EntManager.ToPrettyString(target)}");
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
    public override string Help => "whaledebug <on|off>";
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1 || !bool.TryParse(args[0], out var enabled))
        {
            shell.WriteLine(Help);
            return;
        }

        Config.SetCVar(CCVars.WhaleAdminChatSpam, enabled);
        shell.WriteLine($"Whale admin chat logs {(enabled ? "enabled" : "disabled")}.");
    }
}
