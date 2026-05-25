using Robust.Shared.Configuration;

namespace Content.Shared._SpaceDream.SpeechBarks;

[CVarDefs]
public sealed class SpeechBarkCCVars
{
    public static readonly CVarDef<bool> Enabled =
        CVarDef.Create("speechbarks.enabled", true, CVar.SERVERONLY);

    public static readonly CVarDef<int> MaxBarksPerPhrase =
        CVarDef.Create("speechbarks.max_barks_per_phrase", 64, CVar.SERVERONLY);

    public static readonly CVarDef<float> MinInterval =
        CVarDef.Create("speechbarks.min_interval", 0.12f, CVar.SERVERONLY);

    public static readonly CVarDef<float> DamageInterruptThreshold =
        CVarDef.Create("speechbarks.damage_interrupt_threshold", 15.0f, CVar.SERVERONLY);

    public static readonly CVarDef<bool> ClientEnabled =
        CVarDef.Create("speechbarks.client_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> Volume =
        CVarDef.Create("speechbarks.volume", 1.0f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int> MaxActiveStreams =
        CVarDef.Create("speechbarks.max_active_streams", 20, CVar.CLIENTONLY | CVar.ARCHIVE);
}
