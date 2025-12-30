using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._Goob._Shitmed.Medical.Surgery.Steps;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryBleedsTreatmentStepComponent : Component
{
    [DataField]
    public FixedPoint2 Amount = 5;
}
