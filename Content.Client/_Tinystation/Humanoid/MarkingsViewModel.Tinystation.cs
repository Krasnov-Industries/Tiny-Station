using System;
using System.Collections.Generic;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Humanoid;

public sealed partial class MarkingsViewModel
{
    private const float ColorMatchEpsilon = 0.001f;

    private void SyncSkinColoredMarkings(
        ProtoId<OrganCategoryPrototype> organ,
        OrganProfileData oldProfileData,
        Color newSkinColor,
        HashSet<(ProtoId<OrganCategoryPrototype>, HumanoidVisualLayers)> changedLayers)
    {
        if (!_markings.TryGetValue(organ, out var organMarkings))
            return;

        foreach (var (layer, layerMarkings) in organMarkings)
        {
            var layerChanged = false;

            for (var markingIndex = 0; markingIndex < layerMarkings.Count; markingIndex++)
            {
                var marking = layerMarkings[markingIndex];
                if (!_prototype.TryIndex(marking.MarkingId, out var markingPrototype))
                    continue;

                var oldColors = MarkingColoring.GetMarkingLayerColors(
                    markingPrototype,
                    oldProfileData.SkinColor,
                    oldProfileData.EyeColor,
                    layerMarkings);
                var newColors = MarkingColoring.GetMarkingLayerColors(
                    markingPrototype,
                    newSkinColor,
                    oldProfileData.EyeColor,
                    layerMarkings);

                var count = Math.Min(marking.MarkingColors.Count, Math.Min(oldColors.Count, newColors.Count));
                var updated = marking;
                var markingChanged = false;

                for (var colorIndex = 0; colorIndex < count; colorIndex++)
                {
                    if (!ColorMatches(marking.MarkingColors[colorIndex], oldColors[colorIndex]) ||
                        ColorMatches(marking.MarkingColors[colorIndex], newColors[colorIndex]))
                    {
                        continue;
                    }

                    updated = updated.WithColorAt(colorIndex, newColors[colorIndex]);
                    markingChanged = true;
                }

                if (!markingChanged)
                    continue;

                layerMarkings[markingIndex] = updated;
                layerChanged = true;
            }

            if (layerChanged)
                changedLayers.Add((organ, layer));
        }
    }

    private static bool ColorMatches(Color left, Color right)
    {
        return MathF.Abs(left.R - right.R) <= ColorMatchEpsilon &&
               MathF.Abs(left.G - right.G) <= ColorMatchEpsilon &&
               MathF.Abs(left.B - right.B) <= ColorMatchEpsilon &&
               MathF.Abs(left.A - right.A) <= ColorMatchEpsilon;
    }
}
