using Robust.Shared.Map;

namespace Content.Server._Goobstation.SpaceWhale.Threat;

public sealed class SpaceWhaleExplosionEvent(MapCoordinates epicenter, float totalIntensity, EntityUid? cause) : EntityEventArgs
{
    public MapCoordinates Epicenter { get; } = epicenter;
    public float TotalIntensity { get; } = totalIntensity;
    public EntityUid? Cause { get; } = cause;
}
