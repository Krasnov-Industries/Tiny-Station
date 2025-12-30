// Minimal client UI stub to satisfy augment prototypes.

using System;
using Content.Shared._Goob._Shitmed.Augments.Ui;
using Robust.Client.GameObjects;

namespace Content.Client._Goob._Shitmed.Augments.Ui;

public sealed class AugmentToolPanelMenuBoundUserInterface : BoundUserInterface
{
    public AugmentToolPanelMenuBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
    }
}
