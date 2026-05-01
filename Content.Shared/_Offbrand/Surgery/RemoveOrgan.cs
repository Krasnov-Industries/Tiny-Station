using Content.Shared.Body;
using Content.Shared.Construction;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared._Offbrand.Surgery;

/// <summary>
/// Extracts an organ of the given category from the body and places it in the user's hands or on the floor.
/// </summary>
[DataDefinition]
public sealed partial class RemoveOrgan : IGraphAction
{
    [DataField(required: true)]
    public ProtoId<OrganCategoryPrototype> Category;

    public void PerformAction(EntityUid uid, EntityUid? userUid, IEntityManager entityManager)
    {
        if (!entityManager.TryGetComponent<BodyComponent>(uid, out var body) || body.Organs is null)
            return;

        EntityUid? target = null;
        foreach (var contained in body.Organs.ContainedEntities)
        {
            if (entityManager.TryGetComponent<OrganComponent>(contained, out var organ) && organ.Category == Category)
            {
                target = contained;
                break;
            }
        }

        if (target is not { } organEnt)
            return;

        var containerSys = entityManager.System<SharedContainerSystem>();
        if (!containerSys.Remove(organEnt, body.Organs))
            return;

        entityManager.System<SharedHandsSystem>().PickupOrDrop(userUid, organEnt, dropNear: true);
    }
}
