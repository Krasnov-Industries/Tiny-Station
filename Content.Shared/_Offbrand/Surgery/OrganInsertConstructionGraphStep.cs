using Content.Shared.Body;
using Content.Shared.Construction.Steps;
using Robust.Shared.Prototypes;

namespace Content.Shared._Offbrand.Surgery;

/// <summary>
/// Construction graph step that accepts a free-standing organ of the given category from the user's hands.
/// Pair this with a <c>store: body_organs</c> field so <see cref="BodySystem"/> picks the insert up via the
/// container event and binds the organ to the body.
/// </summary>
public sealed partial class OrganInsertConstructionGraphStep : ArbitraryInsertConstructionGraphStep
{
    [DataField("organCategory", required: true)]
    public ProtoId<OrganCategoryPrototype> OrganCategory;

    public override bool EntityValid(EntityUid uid, IEntityManager entityManager, IComponentFactory compFactory)
    {
        if (!entityManager.TryGetComponent<OrganComponent>(uid, out var organ))
            return false;

        if (organ.Body is not null)
            return false;

        return organ.Category == OrganCategory;
    }
}
