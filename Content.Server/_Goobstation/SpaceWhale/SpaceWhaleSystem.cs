using Content.Server._Goobstation.SpaceWhale.Threat;
using Content.Shared.Devour.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Server._Goobstation.SpaceWhale;

public sealed partial class SpaceWhaleSystem : EntitySystem
{
    [Dependency] private WhaleThreatSystem _threat = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WhaleSpawnedByComponent, EntityTerminatingEvent>(OnWhaleTerminating);
    }

    private void OnWhaleTerminating(Entity<WhaleSpawnedByComponent> ent, ref EntityTerminatingEvent args)
    {
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/_Goobstation/Ambience/SpaceWhale/whale-death.ogg"), ent.Owner);

        // Devourer.OnGibContents only releases when StomachStorageWhitelist is set,
        // and may not fire at all since we don't have a BodyComponent. Empty manually.
        if (TryComp<DevourerComponent>(ent.Owner, out var devourer) && devourer.Stomach != null)
        {
            foreach (var released in _container.EmptyContainer(devourer.Stomach))
            {
                if (TryComp<WhaleEatenCorpseComponent>(released, out var eaten) && eaten.PreserveInStomach)
                    RemComp<WhaleEatenCorpseComponent>(released);
            }
        }

        _threat.ResetAll("whale death");
    }
}
