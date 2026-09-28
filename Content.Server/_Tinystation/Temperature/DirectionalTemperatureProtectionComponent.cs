namespace Content.Server._Tinystation.Temperature;

/// <summary>
/// Preserves independent heating and cooling resistance for fork species and clothing.
/// </summary>
[RegisterComponent]
[Access(typeof(DirectionalTemperatureProtectionSystem))]
public sealed partial class DirectionalTemperatureProtectionComponent : Component
{
    /// <summary>
    /// Multiplier for heat transferred into the protected entity.
    /// </summary>
    [DataField]
    public float HeatingCoefficient = 1f;

    /// <summary>
    /// Multiplier for heat transferred out of the protected entity.
    /// </summary>
    [DataField]
    public float CoolingCoefficient = 1f;
}
