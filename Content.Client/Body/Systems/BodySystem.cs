using Content.Shared.Body.Systems;
using Content.Shared.Humanoid;
using Content.Shared._Goob._Shitmed.Body.Part;

namespace Content.Client.Body.Systems;

public sealed class BodySystem : SharedBodySystem
{
    protected override void ApplyPartMarkings(EntityUid target, BodyPartAppearanceComponent component)
    {
        // Client visuals are applied in shared system; no extra hooks needed here for now.
    }

    protected override void RemoveBodyMarkings(EntityUid target, BodyPartAppearanceComponent component, HumanoidAppearanceComponent humanoid)
    {
        // Client visuals cleaned up in shared system already.
    }
}
