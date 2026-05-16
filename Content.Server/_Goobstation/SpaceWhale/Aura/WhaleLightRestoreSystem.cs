using System.Linq;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Shared.Timing;

namespace Content.Server._Goobstation.SpaceWhale.Aura;

public sealed partial class WhaleLightRestoreSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPoweredLightSystem _poweredLight = default!;

    private TimeSpan _nextTick;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextTick)
            return;

        _nextTick = _timing.CurTime + TimeSpan.FromSeconds(5);
        var query = EntityQueryEnumerator<WhaleAffectedLightComponent, PoweredLightComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var affected, out var light, out var xform))
        {
            if (affected.RestoreAt > _timing.CurTime)
                continue;

            if (_lookup.GetEntitiesInRange<WhaleAuraComponent>(xform.Coordinates, 8f).Any())
                continue;

            _poweredLight.SetState(uid, true, light);
            RemComp<WhaleAffectedLightComponent>(uid);
        }
    }
}
