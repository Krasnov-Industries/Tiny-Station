using Content.Shared.CCVar;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Server._Goobstation.SpaceWhale.Aura;

public sealed partial class WhaleAuraSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedPoweredLightSystem _poweredLight = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<WhaleAuraComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var aura, out var xform))
        {
            if (aura.NextTick > now)
                continue;

            aura.NextTick = now + TimeSpan.FromSeconds(1);
            TickAura(uid, aura, xform);
        }
    }

    private void TickAura(EntityUid uid, WhaleAuraComponent aura, TransformComponent xform)
    {
        var radius = _cfg.GetCVar(CCVars.WhaleAuraRadius);
        foreach (var light in _lookup.GetEntitiesInRange<PoweredLightComponent>(xform.Coordinates, radius))
        {
            if (light.Comp.On)
            {
                var affected = EnsureComp<WhaleAffectedLightComponent>(light.Owner);
                affected.RestoreAt = _timing.CurTime + TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.WhaleLightRestoreSeconds));
                _poweredLight.SetState(light.Owner, false, light.Comp);
            }
            else if (TryComp<WhaleAffectedLightComponent>(light.Owner, out var affected))
            {
                affected.RestoreAt = _timing.CurTime + TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.WhaleLightRestoreSeconds));
            }
        }

    }
}
