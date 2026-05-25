using Content.Shared._SpaceDream.SpeechBarks;
using Content.Shared._SpaceDream.SpeechBarks.Components;
using Content.Shared._SpaceDream.SpeechBarks.Events;
using Content.Shared._SpaceDream.SpeechBarks.Prototypes;
using Content.Shared.Chat;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._SpaceDream.SpeechBarks.EntitySystems;

public sealed partial class SpeechBarksSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private MobThresholdSystem _mobThreshold = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private bool _enabled;
    private int _maxBarksPerPhrase;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EntitySpokeEvent>(OnEntitySpoke);

        _cfg.OnValueChanged(SpeechBarkCCVars.Enabled, value => _enabled = value, true);
        _cfg.OnValueChanged(SpeechBarkCCVars.MaxBarksPerPhrase, value => _maxBarksPerPhrase = Math.Max(0, value), true);
    }

    private void OnEntitySpoke(EntitySpokeEvent args)
    {
        if (!_enabled || args.Channel != null)
            return;

        if (!TryComp<SpeechBarksComponent>(args.Source, out var comp) ||
            comp.BarkPrototype == null ||
            !_proto.TryIndex(comp.BarkPrototype.Value, out var bark))
        {
            return;
        }

        var text = args.ObfuscatedMessage ?? args.Message;
        if (string.IsNullOrWhiteSpace(text))
            return;

        var condition = GetCondition(args.Source);
        if (condition.Silent)
            return;

        var segments = SpeechBarkProsody.BuildPlaybackSegments(text, bark, _maxBarksPerPhrase, condition);
        if (segments.Length == 0)
            return;

        var isWhisper = args.ObfuscatedMessage != null;
        var ev = new PlaySpeechBarksEvent(
            GetNetEntity(args.Source),
            bark.Sound,
            segments,
            isWhisper,
            isWhisper ? 6f : 15f);

        RaiseNetworkEvent(ev, Filter.Pvs(args.Source, entityManager: EntityManager));
    }

    private SpeechBarkCondition GetCondition(EntityUid source)
    {
        if (TryComp<MobStateComponent>(source, out var mobState))
        {
            if (mobState.CurrentState == MobState.Dead)
                return SpeechBarkCondition.SilentCondition;

            if (mobState.CurrentState == MobState.Critical)
                return SpeechBarkCondition.CriticalCondition;
        }

        var severity = 0f;
        if (TryComp<DamageableComponent>(source, out var damageable))
        {
#pragma warning disable CS0618 // We only use this as a soft audio expression modifier.
            var totalDamage = _damage.GetTotalDamage((source, damageable));
#pragma warning restore CS0618

            if (_mobThreshold.TryGetIncapPercentage(source, totalDamage, out var percentage))
                severity = Math.Clamp(percentage.Value.Float(), 0f, 1f);
        }

        return severity switch
        {
            >= 0.75f => SpeechBarkCondition.HeavyDamageCondition,
            >= 0.35f => SpeechBarkCondition.LightDamageCondition,
            _ => SpeechBarkCondition.HealthyCondition,
        };
    }
}
