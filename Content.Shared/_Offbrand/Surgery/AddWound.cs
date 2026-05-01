using Content.Shared._Offbrand.Organs;
using Content.Shared._Offbrand.Wounds;
using Content.Shared.Body;
using Content.Shared.Construction;
using Content.Shared.Damage;
using Robust.Shared.Prototypes;

namespace Content.Shared._Offbrand.Surgery;

[DataDefinition]
public sealed partial class AddWound : IGraphAction
{
    [DataField(required: true)]
    public EntProtoId Wound;

    [DataField(required: true)]
    public DamageSpecifier Damages;

    /// <summary>
    /// The organ category to attach the wound to. Surgical wounds default to the torso, since that's where
    /// the surgery graph is operating. Override for branches that act on other limbs/heads.
    /// </summary>
    [DataField]
    public ProtoId<OrganCategoryPrototype> Category = "Torso";

    public void PerformAction(EntityUid uid, EntityUid? userUid, IEntityManager entityManager)
    {
        if (!entityManager.TryGetComponent<WoundableBodyComponent>(uid, out var woundable))
            return;

        if (!entityManager.System<BodySystem>()
                .TryGetOrgansWithCategoryAndComponent<WoundableOrganComponent>(uid, out var organs, Category))
            return;

        entityManager.System<WoundableSystem>()
            .TryWound((uid, woundable), organs[0], Wound, Damages);
    }
}
