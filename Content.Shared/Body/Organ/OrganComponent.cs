using Content.Shared._Goob._Shitmed.Medical.Surgery.Tools;
using Content.Shared.Body.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared.Body.Organ;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
//[Access(typeof(SharedBodySystem))] // Shitmed compatibility - allow other systems to mutate organs
public sealed partial class OrganComponent : Component, ISurgeryToolComponent
{
    /// <summary>
    /// Relevant body this organ is attached to.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Body;
}
