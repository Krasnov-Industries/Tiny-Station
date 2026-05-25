using Content.Shared._SpaceDream.SpeechBarks;
using Content.Shared._SpaceDream.SpeechBarks.Components;
using Content.Shared._SpaceDream.SpeechBarks.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Server._SpaceDream.SpeechBarks.EntitySystems;

public sealed partial class SpeechBarksInterruptSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;

    private float _damageInterruptThreshold;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpeechBarksComponent, DamageDealtEvent>(OnDamageDealt);
        SubscribeLocalEvent<SpeechBarksComponent, MobStateChangedEvent>(OnMobStateChanged);

        _cfg.OnValueChanged(SpeechBarkCCVars.DamageInterruptThreshold, value => _damageInterruptThreshold = value, true);
    }

    private void OnDamageDealt(Entity<SpeechBarksComponent> ent, ref DamageDealtEvent args)
    {
        if (args.Damage.GetTotal().Float() < _damageInterruptThreshold)
            return;

        RaiseInterrupt(ent.Owner, SpeechBarkInterruptKind.Damage);
    }

    private void OnMobStateChanged(Entity<SpeechBarksComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        RaiseInterrupt(ent.Owner, SpeechBarkInterruptKind.Death);
    }

    private void RaiseInterrupt(EntityUid source, SpeechBarkInterruptKind kind)
    {
        RaiseNetworkEvent(new InterruptSpeechBarksEvent(GetNetEntity(source), kind), Filter.Pvs(source, entityManager: EntityManager));
    }
}
