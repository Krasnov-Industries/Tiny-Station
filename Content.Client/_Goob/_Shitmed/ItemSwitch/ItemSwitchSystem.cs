// Goob import.
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Items;
using Content.Client._Goob._Shitmed.ItemSwitch.UI;
using Content.Shared._Goob._Shitmed.ItemSwitch;
using Content.Shared._Goob._Shitmed.ItemSwitch.Components;
using Robust.Client.GameObjects;

namespace Content.Client._Goob._Shitmed.ItemSwitch;

public sealed class ItemSwitchSystem : SharedItemSwitchSystem
{
    public override void Initialize()
    {
        base.Initialize();

        Subs.ItemStatus<ItemSwitchComponent>(ent => new ItemSwitchStatusControl(ent));
        SubscribeLocalEvent<ItemSwitchComponent, AfterAutoHandleStateEvent>(OnChanged);
    }

    private void OnChanged(Entity<ItemSwitchComponent> ent, ref AfterAutoHandleStateEvent args) => UpdateVisuals(ent, ent.Comp.State);

    protected override void UpdateVisuals(Entity<ItemSwitchComponent> ent, string key)
    {
        base.UpdateVisuals(ent, key);
        if (TryComp(ent, out SpriteComponent? sprite) && ent.Comp.States.TryGetValue(key, out var state))
            if (state.Sprite != null)
                sprite.LayerSetSprite(0, state.Sprite);
    }
}
