using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Tinystation.Nicotine.EntityEffects;

public sealed partial class NicotineEffect : EntityEffectBase<NicotineEffect>
{
    /// <summary>
    ///     Exposure added by 1u of nicotine. A full 10u cigarette is about 1 exposure.
    /// </summary>
    [DataField]
    public float ExposurePerUnit = 0.1f;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString("nicotine-effect-guidebook");
}
