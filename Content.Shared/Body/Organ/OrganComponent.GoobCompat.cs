// Shitmed compatibility extension.

using System.Collections.Generic;
using Content.Shared._Goob._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Goob._Shitmed.Medical.Surgery.Tools;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Body.Organ;

public sealed partial class OrganComponent
{
    [DataField]
    public string ToolName { get; set; } = "An organ";

    [DataField]
    public bool? Used { get; set; } = null;

    [DataField]
    public float Speed { get; set; } = 1f;

    [DataField("slotId"), AutoNetworkedField]
    public string SlotId = string.Empty;

    [DataField("organSeverity"), AutoNetworkedField]
    public OrganSeverity OrganSeverity = OrganSeverity.Normal;

    [DataField("integrity"), AutoNetworkedField]
    public FixedPoint2 OrganIntegrity = FixedPoint2.Zero;

    [DataField("intCap"), AutoNetworkedField]
    public FixedPoint2 IntegrityCap = FixedPoint2.Zero;

    /// <summary>
    /// Runtime modifiers applied by traumas / effects.
    /// </summary>
    [DataField]
    public Dictionary<(string Identifier, EntityUid Owner), FixedPoint2> IntegrityModifiers = new();

    [DataField("integrityThresholds"), AutoNetworkedField]
    public Dictionary<OrganSeverity, FixedPoint2> IntegrityThresholds = new()
    {
        { OrganSeverity.Normal, FixedPoint2.Zero },
        { OrganSeverity.Damaged, FixedPoint2.Zero },
        { OrganSeverity.Destroyed, FixedPoint2.Zero },
    };

    [DataField("removable"), AutoNetworkedField]
    public bool Removable = true;

    [DataField("enabled"), AutoNetworkedField]
    public bool Enabled = true;

    [DataField, AlwaysPushInheritance]
    public ComponentRegistry? OnAdd;

    [DataField, AlwaysPushInheritance]
    public ComponentRegistry? OnRemove;

    [DataField, AutoNetworkedField]
    public EntityUid? OriginalBody;

    [DataField("organDestroyedSound")]
    public SoundSpecifier OrganDestroyedSound = new SoundPathSpecifier("/Audio/Effects/alert.ogg");
}
