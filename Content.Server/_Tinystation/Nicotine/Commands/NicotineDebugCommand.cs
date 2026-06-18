using Content.Server.Administration;
using Content.Server._Tinystation.Nicotine.EntitySystems;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Tinystation.Nicotine.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed partial class NicotineDebugCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entManager = default!;

    public string Command => "nicotinedebug";
    public string Description => "Sets nicotine addiction debug state on an entity.";
    public string Help => "Usage: nicotinedebug <target> <status|clear|exposure|addicted|craving|mild|severe|suppress|cure> [amount]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!NetEntity.TryParse(args[0], out var netEntity) || !_entManager.TryGetEntity(netEntity, out var uid))
        {
            shell.WriteLine("Invalid entity id.");
            return;
        }

        var amount = 1f;
        if (args.Length >= 3 && (!float.TryParse(args[2], out amount) || !float.IsFinite(amount)))
        {
            shell.WriteLine("Invalid amount.");
            return;
        }

        var nicotine = _entManager.System<NicotineSystem>();
        var ok = nicotine.TryRunDebugCommand(uid.Value, args[1], amount, out var message);
        shell.WriteLine(message);

        if (!ok)
            shell.WriteLine(Help);
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 2)
        {
            return CompletionResult.FromHintOptions(
                new[] { "status", "clear", "exposure", "addicted", "craving", "mild", "severe", "suppress", "cure" },
                "debug mode");
        }

        if (args.Length == 3)
            return CompletionResult.FromHint("amount");

        return CompletionResult.Empty;
    }
}
