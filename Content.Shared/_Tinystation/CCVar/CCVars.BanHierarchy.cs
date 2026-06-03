using Robust.Shared.Configuration;

// Tinystation added - ban rank hierarchy CVar, kept out of vanilla CCVars to avoid merge conflicts
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     Ordered list of admin rank names, from lowest to highest, used for the ban hierarchy:
    ///     an admin may only ban someone whose rank is strictly below their own. Equal or higher
    ///     ranks cannot be banned. The console host login (<see cref="ConsoleLoginHostUser"/>)
    ///     always sits above every rank listed here.
    /// </summary>
    public static readonly CVarDef<string> GameBanRankOrder =
        CVarDef.Create("game.ban_rank_order", "Стажёр,Админ,Гейм-мастер,Основатель", CVar.SERVERONLY);
}
