using Content.Shared.Administration;
using Content.Shared.CCVar.CVarAccess;
using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    [CVarControl(AdminFlags.Server)]
    public static readonly CVarDef<bool> WhaleEnabled =
        CVarDef.Create("whale.enabled", true, CVar.SERVER);

    public static readonly CVarDef<float> WhaleThreatDecay =
        CVarDef.Create("whale.threat_decay", 1f, CVar.SERVER);

    public static readonly CVarDef<float> WhaleThreatMax =
        CVarDef.Create("whale.threat_max", 500f, CVar.SERVER);

    public static readonly CVarDef<float> WhaleThreatSpawnAt =
        CVarDef.Create("whale.threat_spawn_at", 500f, CVar.SERVER);

    public static readonly CVarDef<float> WhaleThreatWarningAt =
        CVarDef.Create("whale.threat_warning_at", 250f, CVar.SERVER);

    public static readonly CVarDef<float> WhaleAliveCueInterval =
        CVarDef.Create("whale.alive_cue_interval", 60f, CVar.SERVER);

    public static readonly CVarDef<float> WhaleNoiseMaxAge =
        CVarDef.Create("whale.noise_max_age", 30f, CVar.SERVER);

    public static readonly CVarDef<int> WhaleNoiseMaxEntries =
        CVarDef.Create("whale.noise_max_entries", 20, CVar.SERVER);

    public static readonly CVarDef<float> WhaleNoiseAggregateRadius =
        CVarDef.Create("whale.noise_aggregate_radius", 5f, CVar.SERVER);

    public static readonly CVarDef<float> WhaleNoiseAggregateWindow =
        CVarDef.Create("whale.noise_aggregate_window", 2f, CVar.SERVER);

    public static readonly CVarDef<float> WhaleConsumeHeal =
        CVarDef.Create("whale.consume_heal", 300f, CVar.SERVER);

    public static readonly CVarDef<float> WhaleStomachCleanupSeconds =
        CVarDef.Create("whale.stomach_cleanup_sec", 300f, CVar.SERVER);

    public static readonly CVarDef<float> WhaleAwakenDistance =
        CVarDef.Create("whale.awaken_distance", 2000f, CVar.SERVER);

    public static readonly CVarDef<float> WhaleAwakenDistanceTime =
        CVarDef.Create("whale.awaken_distance_time", 600f, CVar.SERVER);

    public static readonly CVarDef<float> WhaleAwakenExplosion =
        CVarDef.Create("whale.awaken_explosion", 50f, CVar.SERVER);

    public static readonly CVarDef<float> WhaleAwakenNukeForce =
        CVarDef.Create("whale.awaken_nuke_force", 200f, CVar.SERVER);

    public static readonly CVarDef<float> WhaleAuraRadius =
        CVarDef.Create("whale.aura_radius", 8f, CVar.SERVER);

    public static readonly CVarDef<float> WhaleLightRestoreSeconds =
        CVarDef.Create("whale.light_restore", 60f, CVar.SERVER);

    public static readonly CVarDef<bool> WhaleAdminChatSpam =
        CVarDef.Create("whale.admin_chatspam", false, CVar.SERVERONLY);
}
