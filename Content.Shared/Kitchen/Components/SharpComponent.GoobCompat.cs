// Shitmed compatibility helpers for ghetto surgery.

using Robust.Shared.Serialization;

namespace Content.Shared.Kitchen.Components;

public sealed partial class SharpComponent
{
    [DataField]
    public bool HadSurgeryTool;

    [DataField]
    public bool HadScalpel;

    [DataField]
    public bool HadBoneSaw;
}
