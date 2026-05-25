using Content.Shared._SpaceDream.SpeechBarks.Components;
using Content.Shared._SpaceDream.SpeechBarks.Prototypes;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._SpaceDream.SpeechBarks.EntitySystems;

public sealed partial class SpeechBarksProfileSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;

    private readonly List<BarkPrototype> _candidates = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<SpeechBarksComponent, SexChangedEvent>(OnSexChanged);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        AssignBark(args.Mob, args.Profile.Voice);
    }

    private void OnSexChanged(Entity<SpeechBarksComponent> ent, ref SexChangedEvent args)
    {
        if (ent.Comp.BarkPrototype != null && !ent.Comp.RandomlyAssigned)
            return;

        AssignRandomBark(ent.Owner);
    }

    private void AssignBark(EntityUid uid, ProtoId<BarkPrototype>? profileBark)
    {
        if (profileBark != null && _proto.HasIndex(profileBark))
        {
            var comp = EnsureComp<SpeechBarksComponent>(uid);
            comp.BarkPrototype = profileBark;
            comp.RandomlyAssigned = false;
            return;
        }

        AssignRandomBark(uid);
    }

    private void AssignRandomBark(EntityUid uid)
    {
        _candidates.Clear();

        foreach (var bark in _proto.EnumeratePrototypes<BarkPrototype>())
        {
            if (bark.RoundStart)
                _candidates.Add(bark);
        }

        if (_candidates.Count == 0)
            return;

        var comp = EnsureComp<SpeechBarksComponent>(uid);
        comp.BarkPrototype = _random.Pick(_candidates).ID;
        comp.RandomlyAssigned = true;
    }
}
