using Robust.Shared.GameStates;

namespace Content.Shared._Tinystation.Nicotine.Components;

/// <summary>
///     Tracks nicotine dependence. Withdrawal stage is calculated from LastNicotineTime,
///     so we do not need to continuously increment counters.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NicotineAddictionComponent : Component
{
    [DataField]
    public TimeSpan LastNicotineTime;

    [DataField]
    public TimeSpan WithdrawalSuppressedUntil;

    [DataField]
    public float CureProgress;

    [DataField]
    public TimeSpan NextPopupTime;

    [DataField]
    public int Severity = 1;

    [DataField]
    public bool HasReceivedNicotine;

    /// <summary>
    ///     Last observed nicotine amount in the bloodstream solution.
    ///     Used to reset withdrawal as soon as smoked/injected nicotine enters blood,
    ///     without waiting for the next metabolism tick.
    /// </summary>
    [DataField]
    public float LastKnownBloodNicotine;
}
