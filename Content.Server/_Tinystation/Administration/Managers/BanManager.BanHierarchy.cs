using System.Threading.Tasks;
using Content.Shared.CCVar;
using Robust.Shared.Network;

// Tinystation added - rank-based ban protection, kept in a partial to minimise vanilla edits
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Administration.Managers;

public sealed partial class BanManager
{
    /// <summary>
    ///     Returns the ban-hierarchy level of a user. Higher means more protected.
    ///     0 = regular player (not an admin). The console host login is the top of the hierarchy.
    /// </summary>
    private async Task<int> GetBanProtectionLevelAsync(NetUserId? userId, string? userName)
    {
        // The console host login (config) always outranks every database rank.
        var hostUser = _cfg.GetCVar(CCVars.ConsoleLoginHostUser);
        if (!string.IsNullOrEmpty(hostUser) && userName == hostUser)
            return int.MaxValue;

        if (userId == null)
            return 0;

        var adminData = await _db.GetAdminDataForAsync(userId.Value);
        if (adminData?.AdminRank == null)
            return 0;

        var order = _cfg.GetCVar(CCVars.GameBanRankOrder)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var i = 0; i < order.Length; i++)
        {
            if (string.Equals(order[i], adminData.AdminRank.Name, StringComparison.Ordinal))
                return i + 1;
        }

        // An admin whose rank is not listed in the hierarchy still gets a baseline protected level.
        return 1;
    }

    /// <summary>
    ///     Enforces the rank hierarchy for a ban: a player may only be banned by an admin of a
    ///     strictly higher rank. Returns false (and alerts admins) when the ban is not allowed.
    /// </summary>
    private async Task<bool> CheckBanHierarchyAsync(CreateBanInfo banInfo)
    {
        // System / console-issued bans (no responsible admin) bypass the hierarchy.
        if (banInfo.BanningAdmin == null)
            return true;

        var banningName = (await _db.GetPlayerRecordByUserId(banInfo.BanningAdmin.Value))?.LastSeenUserName;
        var banningLevel = await GetBanProtectionLevelAsync(banInfo.BanningAdmin, banningName);

        foreach (var (userId, userName) in banInfo.Users)
        {
            var targetLevel = await GetBanProtectionLevelAsync(userId, userName);

            // Only admins are protected (level > 0); regular players are banned as usual.
            if (targetLevel > 0 && banningLevel <= targetLevel)
            {
                _sawmill.Warning(
                    $"Ban denied by hierarchy: {banningName} (level {banningLevel}) tried to ban {userName} (level {targetLevel}).");
                _chat.SendAdminAlert(Loc.GetString("ban-hierarchy-denied", ("target", userName)));
                return false;
            }
        }

        return true;
    }
}
