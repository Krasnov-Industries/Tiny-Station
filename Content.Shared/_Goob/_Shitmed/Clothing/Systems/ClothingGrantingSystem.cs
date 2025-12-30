// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Goob._Shitmed.Clothing.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Shared._Goob._Shitmed.Clothing.Systems;

/// <summary>
/// Applies temporary components/tags to the wearer while specific clothing is equipped.
/// </summary>
public sealed class ClothingGrantingSystem : EntitySystem
{
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;
    [Dependency] private readonly TagSystem _tags = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ClothingGrantComponentComponent, GotEquippedEvent>(OnCompEquip);
        SubscribeLocalEvent<ClothingGrantComponentComponent, GotUnequippedEvent>(OnCompUnequip);

        SubscribeLocalEvent<ClothingGrantTagComponent, GotEquippedEvent>(OnTagEquip);
        SubscribeLocalEvent<ClothingGrantTagComponent, GotUnequippedEvent>(OnTagUnequip);
    }

    private void OnCompEquip(Entity<ClothingGrantComponentComponent> ent, ref GotEquippedEvent args)
    {
        foreach (var (name, entry) in ent.Comp.Components)
        {
            var reg = _factory.GetRegistration(name);
            var had = EntityManager.HasComponent(args.Equipee, reg.Type);

            // Add missing component with registry data.
            var temp = new ComponentRegistry { { name, entry } };
            EntityManager.AddComponents(args.Equipee, temp, removeExisting: false);

            ent.Comp.Active[name] = !had;
        }
    }

    private void OnCompUnequip(Entity<ClothingGrantComponentComponent> ent, ref GotUnequippedEvent args)
    {
        foreach (var (name, added) in ent.Comp.Active)
        {
            if (!added || !ent.Comp.Components.TryGetValue(name, out var entry))
                continue;

            var temp = new ComponentRegistry { { name, entry } };
            EntityManager.RemoveComponents(args.Equipee, temp);
        }

        ent.Comp.Active.Clear();
    }

    private void OnTagEquip(Entity<ClothingGrantTagComponent> ent, ref GotEquippedEvent args)
    {
        if (_tags.HasTag(args.Equipee, ent.Comp.Tag))
        {
            ent.Comp.IsActive = false;
            return;
        }

        ent.Comp.IsActive = _tags.AddTag(args.Equipee, ent.Comp.Tag);
    }

    private void OnTagUnequip(Entity<ClothingGrantTagComponent> ent, ref GotUnequippedEvent args)
    {
        if (!ent.Comp.IsActive)
            return;

        _tags.RemoveTag(args.Equipee, ent.Comp.Tag);
        ent.Comp.IsActive = false;
    }
}
