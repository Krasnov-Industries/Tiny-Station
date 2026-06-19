using Robust.Shared.GameStates;

namespace Content.Shared._Tinystation.Nicotine.Components;

/// <summary>
///     Lazy nicotine exposure tracker for non-addicted characters.
///     Decay is applied only when nicotine is consumed again.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NicotineExposureComponent : Component
{
    [DataField]
    public float Exposure;

    [DataField]
    public TimeSpan LastExposureUpdate;
}
