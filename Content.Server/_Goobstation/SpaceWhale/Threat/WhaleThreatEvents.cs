using Robust.Shared.Map;

namespace Content.Server._Goobstation.SpaceWhale.Threat;

public sealed class WhaleThreatChangedEvent(float newThreat) : EntityEventArgs
{
    public float NewThreat { get; } = newThreat;
}

public sealed class WhaleAwakenedEvent(string reason) : EntityEventArgs
{
    public string Reason { get; } = reason;
}

public sealed class WhaleNoiseEvent(EntityCoordinates pos, float intensity) : EntityEventArgs
{
    public EntityCoordinates Position { get; } = pos;
    public float Intensity { get; } = intensity;
}

public sealed class SpaceWhaleExplosionEvent(MapCoordinates epicenter, float totalIntensity, EntityUid? cause) : EntityEventArgs
{
    public MapCoordinates Epicenter { get; } = epicenter;
    public float TotalIntensity { get; } = totalIntensity;
    public EntityUid? Cause { get; } = cause;
}
