using Content.Shared.Weapons.Ranged.Components;

namespace Content.Shared._Goob._Shitmed.Weapons.Ranged.Events;

public sealed class GunShotBodyEvent(EntityUid gunUid, GunComponent gun) : EntityEventArgs
{
    public EntityUid GunUid => gunUid;
    public GunComponent Gun => gun;
}
