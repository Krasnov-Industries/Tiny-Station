// Shitmed compatibility helpers to emulate legacy body API.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._Goob._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Goob._Shitmed.Targeting;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Robust.Shared.Containers;

namespace Content.Shared.Body.Systems;

public abstract partial class SharedBodySystem
{
    public bool TryGetRootPart(EntityUid body, [NotNullWhen(true)] out Entity<BodyPartComponent>? rootPart, BodyComponent? bodyComp = null)
    {
        rootPart = null;
        if (!Resolve(body, ref bodyComp, logMissing: false))
            return false;

        if (bodyComp.RootContainer.ContainedEntity is not { } root)
            return false;

        var part = Comp<BodyPartComponent>(root);
        rootPart = (root, part);
        return true;
    }

    public TargetBodyPart GetTargetBodyPart(Entity<BodyPartComponent> part)
    {
        return GetTargetBodyPart(part.Comp.PartType, part.Comp.Symmetry);
    }

    public TargetBodyPart GetTargetBodyPart(BodyPartComponent part)
    {
        return GetTargetBodyPart(part.PartType, part.Symmetry);
    }

    public TargetBodyPart GetTargetBodyPart(Entity<WoundableComponent> woundable)
    {
        if (TryComp(woundable.Owner, out BodyPartComponent? part))
            return GetTargetBodyPart((woundable.Owner, part));

        return TargetBodyPart.Chest;
    }

    private TargetBodyPart GetTargetBodyPart(BodyPartType type, BodyPartSymmetry symmetry)
    {
        return type switch
        {
            BodyPartType.Head => TargetBodyPart.Head,
            BodyPartType.Arm when symmetry == BodyPartSymmetry.Left => TargetBodyPart.LeftArm,
            BodyPartType.Arm when symmetry == BodyPartSymmetry.Right => TargetBodyPart.RightArm,
            BodyPartType.Hand when symmetry == BodyPartSymmetry.Left => TargetBodyPart.LeftHand,
            BodyPartType.Hand when symmetry == BodyPartSymmetry.Right => TargetBodyPart.RightHand,
            BodyPartType.Leg when symmetry == BodyPartSymmetry.Left => TargetBodyPart.LeftLeg,
            BodyPartType.Leg when symmetry == BodyPartSymmetry.Right => TargetBodyPart.RightLeg,
            BodyPartType.Foot when symmetry == BodyPartSymmetry.Left => TargetBodyPart.LeftFoot,
            BodyPartType.Foot when symmetry == BodyPartSymmetry.Right => TargetBodyPart.RightFoot,
            _ => TargetBodyPart.Chest
        };
    }

    public bool TryGetBodyPartOrgans(EntityUid partId, Type organType, [NotNullWhen(true)] out List<(EntityUid Id, OrganComponent Organ)>? organs, BodyPartComponent? part = null)
    {
        organs = null;
        if (!Resolve(partId, ref part, logMissing: false))
            return false;

        var matches = new List<(EntityUid, OrganComponent)>();
        foreach (var (id, organ) in GetPartOrgans(partId, part))
        {
            if (organType.IsInstanceOfType(organ))
                matches.Add((id, organ));
        }

        if (matches.Count == 0)
            return false;

        organs = matches;
        return true;
    }

    public bool CanAttachToSlot(EntityUid partId, string slotId, BodyPartComponent? part = null)
    {
        return Resolve(partId, ref part, logMissing: false)
               && part.CanAttachChildren
               && part.Children.ContainsKey(slotId)
               && Containers.TryGetContainer(partId, GetPartSlotContainerId(slotId), out var container)
               && container is ContainerSlot slot
               && slot.ContainedEntity is null;
    }

    public int GetBodyPartCount(EntityUid body, BodyPartType type, BodyComponent? bodyComp = null)
    {
        return GetBodyChildrenOfType(body, type, bodyComp).Count();
    }

    public bool TryGetPartSlotContainerName(BodyPartType type, [NotNullWhen(true)] out List<string>? containerNames)
    {
        // Modern Tiny doesn't have this mapping; return false to skip.
        containerNames = null;
        return false;
    }

    public bool DetachPart(EntityUid parentPartId, string slotId, EntityUid partId, BodyPartComponent? parentPart = null, BodyPartComponent? part = null)
    {
        if (!Resolve(parentPartId, ref parentPart, logMissing: false))
            return false;

        if (!Containers.TryGetContainer(parentPartId, GetPartSlotContainerId(slotId), out var container))
            return false;

        return Containers.Remove(partId, container);
    }

    public bool TryRemoveOrgan(EntityUid organId, OrganComponent organ)
    {
        if (!Containers.TryGetContainingContainer((organId, Transform(organId), MetaData(organId)), out var container))
            return false;

        if (!Containers.Remove(organId, container))
            return false;

        organ.Body = null;
        Dirty(organId, organ);
        return true;
    }
}
