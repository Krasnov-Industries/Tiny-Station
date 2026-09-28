using Content.Shared.Inventory;
using Content.Shared.Temperature;

namespace Content.Server._Tinystation.Temperature;

public sealed partial class DirectionalTemperatureProtectionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        Subs.SubscribeWithRelay<DirectionalTemperatureProtectionComponent, BeforeHeatExchangeEvent>(OnBeforeHeatExchange, held: false);
    }

    private void OnBeforeHeatExchange(Entity<DirectionalTemperatureProtectionComponent> ent, ref BeforeHeatExchangeEvent args)
    {
        args.HeatTransferModifier *= args.Heating
            ? ent.Comp.HeatingCoefficient
            : ent.Comp.CoolingCoefficient;
    }
}
