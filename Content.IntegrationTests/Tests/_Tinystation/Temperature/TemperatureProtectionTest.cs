using Content.IntegrationTests.Fixtures;
using Content.Server._Tinystation.Temperature;
using Content.Server.Temperature.Systems;
using Content.Shared.Inventory;
using Content.Shared.Temperature.Components;
using Content.Shared.Temperature.HeatContainer;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Tinystation.Temperature;

[TestFixture]
[TestOf(typeof(DirectionalTemperatureProtectionSystem))]
public sealed class TemperatureProtectionTest : GameTest
{
    public override PoolSettings PoolSettings => PsDisconnected;

    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  id: TinyTemperatureTestSubject
  components:
  - type: Temperature
    temperature: 300
    specificHeat: 100
    heatCapacity: 100
    thermalConductivity: 10
  - type: Inventory
  - type: ContainerContainer

- type: entity
  parent: TinyTemperatureTestSubject
  id: TinyTemperatureTestDirectional
  components:
  - type: DirectionalTemperatureProtection
    heatingCoefficient: 0.2
    coolingCoefficient: 0.8

- type: entity
  parent: TinyTemperatureTestSubject
  id: TinyTemperatureTestVanilla
  components:
  - type: TemperatureProtection
    coefficient: 0.5

- type: entity
  parent: TinyTemperatureTestSubject
  id: TinyTemperatureTestDefaultProtection
  components:
  - type: DirectionalTemperatureProtection

- type: entity
  id: TinyTemperatureTestClothing
  components:
  - type: Item
    size: Small
  - type: Clothing
    slots: [HEAD]
  - type: DirectionalTemperatureProtection
    heatingCoefficient: 0.2
    coolingCoefficient: 0.8
""";

    public enum HeatExchangeKind : byte
    {
        FixedTemperature,
        HeatContainer,
        ChangeHeat,
    }

    [Test]
    public Task DirectionalProtectionUsesHeatFlowDirection([Values] HeatExchangeKind exchange)
    {
        return AssertHeatExchange("TinyTemperatureTestDirectional", exchange, 0.2f, 0.8f);
    }

    [Test]
    public Task VanillaProtectionRemainsSymmetric([Values] HeatExchangeKind exchange)
    {
        return AssertHeatExchange("TinyTemperatureTestVanilla", exchange, 0.5f, 0.5f);
    }

    [Test]
    public Task DefaultDirectionalProtectionDoesNotChangeHeatTransfer([Values] HeatExchangeKind exchange)
    {
        return AssertHeatExchange("TinyTemperatureTestDefaultProtection", exchange, 1f, 1f);
    }

    [Test]
    public Task IgnoringResistanceBypassesDirectionalProtection([Values] HeatExchangeKind exchange)
    {
        return AssertHeatExchange("TinyTemperatureTestDirectional", exchange, 1f, 1f, ignoreResistance: true);
    }

    [Test]
    public Task InventoryProtectionAppliesOnlyWhenWorn(
        [Values] HeatExchangeKind exchange,
        [Values("head", "pocket1")] string slot)
    {
        var worn = slot == "head";
        return AssertHeatExchange("TinyTemperatureTestSubject", exchange, worn ? 0.2f : 1f, worn ? 0.8f : 1f, slot);
    }

    private async Task AssertHeatExchange(
        string prototype,
        HeatExchangeKind exchange,
        float heatingCoefficient,
        float coolingCoefficient,
        string slot = null,
        bool ignoreResistance = false)
    {
        EntityUid heated = default;
        EntityUid cooled = default;
        var heatingEquipped = false;
        var coolingEquipped = false;

        await Server.WaitPost(() =>
        {
            // Nullspace entities have no ambient heat exchange or body-temperature regulation.
            // GameTest tracks these entities and deletes them before returning the pair to the pool.
            heated = SSpawn(prototype);
            cooled = SSpawn(prototype);
            if (slot == null)
                return;

            var inventory = SEntMan.System<InventorySystem>();
            heatingEquipped = inventory.TryEquip(heated, SSpawn("TinyTemperatureTestClothing"), slot, silent: true, force: true);
            coolingEquipped = inventory.TryEquip(cooled, SSpawn("TinyTemperatureTestClothing"), slot, silent: true, force: true);
        });

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SComp<TemperatureComponent>(heated).Temperature, Is.EqualTo(300f));
                Assert.That(SComp<TemperatureComponent>(cooled).Temperature, Is.EqualTo(300f));
                Assert.That(SComp<TemperatureComponent>(heated).HeatCapacity, Is.EqualTo(100f));
                Assert.That(SComp<TemperatureComponent>(cooled).HeatCapacity, Is.EqualTo(100f));
                if (slot == null)
                    return;

                Assert.That(heatingEquipped, Is.True, $"Could not equip heating protection in {slot}.");
                Assert.That(coolingEquipped, Is.True, $"Could not equip cooling protection in {slot}.");
            });
        });

        var hotSource = new HeatContainer(100f, 400f);
        var coldSource = new HeatContainer(100f, 200f);
        float heatAdded = 0;
        float heatRemoved = 0;

        await Server.WaitPost(() =>
        {
            var temperature = SEntMan.System<TemperatureSystem>();
            // All three paths exchange +/-100 J before protection. Conduction uses
            // 10 W/K * 100 K * 0.1 s, well below the equilibrium clamp.
            switch (exchange)
            {
                case HeatExchangeKind.FixedTemperature:
                    heatAdded = temperature.ConductHeat(heated, 400f, 0.1f, ignoreHeatResistance: ignoreResistance);
                    heatRemoved = temperature.ConductHeat(cooled, 200f, 0.1f, ignoreHeatResistance: ignoreResistance);
                    break;
                case HeatExchangeKind.HeatContainer:
                    heatAdded = temperature.ConductHeat(heated, ref hotSource, 0.1f, ignoreHeatResistance: ignoreResistance);
                    heatRemoved = temperature.ConductHeat(cooled, ref coldSource, 0.1f, ignoreHeatResistance: ignoreResistance);
                    break;
                case HeatExchangeKind.ChangeHeat:
                    heatAdded = temperature.ChangeHeat(heated, 100f, ignoreResistance);
                    heatRemoved = temperature.ChangeHeat(cooled, -100f, ignoreResistance);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(exchange), exchange, null);
            }
        });

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(heatAdded, Is.EqualTo(100f * heatingCoefficient).Within(0.001f));
                Assert.That(heatRemoved, Is.EqualTo(-100f * coolingCoefficient).Within(0.001f));
                Assert.That(SComp<TemperatureComponent>(heated).Temperature, Is.EqualTo(300f + heatingCoefficient).Within(0.001f));
                Assert.That(SComp<TemperatureComponent>(cooled).Temperature, Is.EqualTo(300f - coolingCoefficient).Within(0.001f));

                if (exchange != HeatExchangeKind.HeatContainer)
                    return;

                // The finite source must lose exactly the energy received by the protected entity.
                Assert.That(hotSource.Temperature, Is.EqualTo(400f - heatingCoefficient).Within(0.001f));
                Assert.That(coldSource.Temperature, Is.EqualTo(200f + coolingCoefficient).Within(0.001f));
            });
        });
    }
}
