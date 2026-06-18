using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Tinystation.Nicotine.EntityEffects;

public sealed partial class CytisineEffect : EntityEffectBase<CytisineEffect>
{
    /// <summary>
    ///     Cure progress gained per default metabolism tick.
    /// </summary>
    [DataField]
    public float CurePerUnit = 1f;

    /// <summary>
    ///     How many seconds of withdrawal suppression one default metabolism tick provides.
    /// </summary>
    [DataField]
    public float SuppressionSecondsPerUnit = 120f;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString("cytisine-effect-guidebook");
}
