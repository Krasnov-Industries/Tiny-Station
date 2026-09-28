using Content.Shared.Inventory;

namespace Content.Shared.Temperature;

/// <summary>
/// This event is raised before heat is exchanged so that the conductance of the exchange can be changed.
/// </summary>
[ByRefEvent]
public record struct BeforeHeatExchangeEvent() : IInventoryRelayEvent
{
    public SlotFlags TargetSlots { get; } = ~SlotFlags.POCKET;

    /// <summary>
    /// A multiplicative modifier for heat transfers on the entity this event is being raised to.
    /// </summary>
    public float HeatTransferModifier = 1f;

    // Tinystation added start - direction for asymmetric heat protection.
    /// <summary>
    /// Whether this exchange adds heat to the entity. False means heat is being removed.
    /// </summary>
    public bool Heating { get; init; }
    // Tinystation added end
}

/// <summary>
/// This event is raised after heat is exchanged to inform other systems that temperature has changed.
/// </summary>
/// <param name="CurrentTemperature">Current temperature of this entity.</param>
/// <param name="LastTemperature">Previous temperature of this entity.</param>
[ByRefEvent]
public record struct TemperatureChangedEvent(float CurrentTemperature, float LastTemperature)
{
    public readonly float CurrentTemperature = CurrentTemperature;
    public readonly float LastTemperature = LastTemperature;
    public readonly float TemperatureDelta = CurrentTemperature - LastTemperature;
}
