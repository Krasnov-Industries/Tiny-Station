namespace Content.Server._EE.Flash.Components;

/// <summary>
/// Makes the entity suffer longer flashes and temporary blurry vision when flashed.
/// Used for races with sensitive eyes.
/// </summary>
[RegisterComponent]
public sealed partial class EyeDamageOnFlashingComponent : Component
{
    /// <summary>
    /// Multiplier applied to the flash duration against this entity.
    /// </summary>
    [DataField]
    public float FlashDurationMultiplier = 1.5f;

    /// <summary>
    /// Chance (0..1) to receive temporary blur on each successful flash.
    /// </summary>
    [DataField]
    public float BlurChance = 0.3f;

    /// <summary>
    /// Amount of temporary blur added if <see cref="BlurChance"/> rolls succeed.
    /// </summary>
    [DataField]
    public float BlurAmount = 1f;

    /// <summary>
    /// Maximum temporary blur this component may add.
    /// </summary>
    [DataField]
    public float MaxBlur = 3f;

    /// <summary>
    /// Seconds after the last successful blur roll before blur starts fading.
    /// </summary>
    [DataField]
    public float BlurDecayDelay = 10f;

    /// <summary>
    /// Temporary blur removed per second after <see cref="BlurDecayDelay"/>.
    /// </summary>
    [DataField]
    public float BlurDecayPerSecond = 0.12f;

    [ViewVariables]
    public float CurrentBlur;

    [ViewVariables]
    public TimeSpan BlurDecayStartsAt;
}

[RegisterComponent]
public sealed partial class ActiveEyeDamageOnFlashingComponent : Component;
