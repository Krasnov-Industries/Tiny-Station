using Content.Shared._Tinystation.Nicotine.EntityEffects;
using Content.Server.Popups;
using Content.Shared._Tinystation.Nicotine.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Tinystation.Nicotine.EntitySystems;

public sealed partial class NicotineSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private MovementModStatusSystem _movement = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;

    private static readonly EntProtoId NicotineSpeed = "StatusEffectNicotineSpeed";
    private static readonly EntProtoId NicotineStamina = "StatusEffectNicotineStamina";
    private static readonly EntProtoId NicotineWithdrawalSpeed = "StatusEffectNicotineWithdrawalSpeed";
    private static readonly EntProtoId Drowsiness = "StatusEffectDrowsiness";
    private static readonly EntProtoId ForcedSleeping = "StatusEffectForcedSleeping";

    private const float UpdateInterval = 20f;
    private const float AddictionThreshold = 6f;
    private const float ExposureDecayPerMinute = 1f / 18f;
    private const float CureThreshold = 30f;
    private const float NicotineCurePenalty = 2f;

    private static readonly TimeSpan CravingDelay = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan MildWithdrawalDelay = TimeSpan.FromMinutes(25);
    private static readonly TimeSpan SevereWithdrawalDelay = TimeSpan.FromMinutes(40);
    private static readonly TimeSpan NicotineBuffDuration = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan NicotineStaminaDuration = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan DrowsinessRemoveTime = TimeSpan.FromSeconds(2);

    private float _accumulator;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MetaDataComponent, EntityEffectEvent<NicotineEffect>>(OnNicotineEffect);
        SubscribeLocalEvent<MetaDataComponent, EntityEffectEvent<CytisineEffect>>(OnCytisineEffect);
        SubscribeLocalEvent<NicotineAddictionComponent, ComponentStartup>(OnAddictionStartup);
        SubscribeLocalEvent<NicotineAddictionComponent, RefreshStaminaCritThresholdEvent>(OnRefreshStaminaThreshold);
    }

    private void OnAddictionStartup(Entity<NicotineAddictionComponent> ent, ref ComponentStartup args)
    {
        var now = _timing.CurTime;

        if (ent.Comp.LastNicotineTime == default)
            ent.Comp.LastNicotineTime = now;

        if (ent.Comp.NextPopupTime == default)
            ent.Comp.NextPopupTime = now + TimeSpan.FromMinutes(15);
    }

    private void OnNicotineEffect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<NicotineEffect> args)
    {
        var now = _timing.CurTime;

        ApplyNicotineBuff(ent.Owner);
        _status.TryRemoveTime(ent.Owner, Drowsiness, DrowsinessRemoveTime * args.Scale);
        _status.TryRemoveTime(ent.Owner, ForcedSleeping, TimeSpan.FromSeconds(args.Scale));

        if (TryComp<NicotineAddictionComponent>(ent.Owner, out var addiction))
        {
            var stage = GetWithdrawalStage(addiction, now);

            addiction.LastNicotineTime = now;
            addiction.HasReceivedNicotine = true;
            addiction.WithdrawalSuppressedUntil = now;
            addiction.NextPopupTime = now + TimeSpan.FromMinutes(8);
            addiction.CureProgress = MathF.Max(0f, addiction.CureProgress - NicotineCurePenalty * args.Scale);
            Dirty(ent.Owner, addiction);

            ClearWithdrawal(ent.Owner);

            if (stage >= NicotineWithdrawalStage.Craving)
                _popup.PopupEntity(Loc.GetString("nicotine-addiction-relieved"), ent.Owner, ent.Owner, PopupType.Small);

            return;
        }

        AddExposure(ent.Owner, args.Effect.ExposurePerUnit * args.Scale, now);
    }

    private void OnCytisineEffect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<CytisineEffect> args)
    {
        if (!TryComp<NicotineAddictionComponent>(ent.Owner, out var addiction))
            return;

        var now = _timing.CurTime;
        var amount = args.Scale;
        var suppression = TimeSpan.FromSeconds(args.Effect.SuppressionSecondsPerUnit * amount);

        addiction.CureProgress += args.Effect.CurePerUnit * amount;
        var newSuppressedUntil = now + suppression;
        if (newSuppressedUntil > addiction.WithdrawalSuppressedUntil)
            addiction.WithdrawalSuppressedUntil = newSuppressedUntil;

        addiction.NextPopupTime = now + TimeSpan.FromMinutes(5);
        Dirty(ent.Owner, addiction);
        ClearWithdrawal(ent.Owner);

        if (addiction.CureProgress >= CureThreshold)
        {
            RemComp<NicotineAddictionComponent>(ent.Owner);
            ClearWithdrawal(ent.Owner);
            _popup.PopupEntity(Loc.GetString("nicotine-addiction-cured"), ent.Owner, ent.Owner, PopupType.Medium);
            return;
        }

        _popup.PopupEntity(Loc.GetString("cytisine-withdrawal-suppressed"), ent.Owner, ent.Owner, PopupType.Small);
    }

    private void AddExposure(EntityUid uid, float exposureToAdd, TimeSpan now)
    {
        var exposure = EnsureComp<NicotineExposureComponent>(uid);

        if (exposure.LastExposureUpdate != default)
        {
            var minutes = Math.Max(0, (now - exposure.LastExposureUpdate).TotalMinutes);
            exposure.Exposure = MathF.Max(0f, exposure.Exposure - (float) minutes * ExposureDecayPerMinute);
        }

        exposure.Exposure += exposureToAdd;
        exposure.LastExposureUpdate = now;

        if (exposure.Exposure >= AddictionThreshold)
        {
            RemComp<NicotineExposureComponent>(uid);
            var addiction = EnsureComp<NicotineAddictionComponent>(uid);
            addiction.LastNicotineTime = now;
            addiction.HasReceivedNicotine = true;
            addiction.NextPopupTime = now + TimeSpan.FromMinutes(12);
            Dirty(uid, addiction);
            _popup.PopupEntity(Loc.GetString("nicotine-addiction-developed"), uid, uid, PopupType.MediumCaution);
            return;
        }

        Dirty(uid, exposure);
    }

    private void ApplyNicotineBuff(EntityUid uid)
    {
        if (HasComp<MovementSpeedModifierComponent>(uid))
            _movement.TryUpdateMovementSpeedModDuration(uid, NicotineSpeed, NicotineBuffDuration, 1.03f, 1.03f);

        if (HasComp<StaminaComponent>(uid))
            _status.TrySetStatusEffectDuration(uid, NicotineStamina, NicotineStaminaDuration);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < UpdateInterval)
            return;

        _accumulator -= UpdateInterval;
        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<NicotineAddictionComponent>();
        while (query.MoveNext(out var uid, out var addiction))
        {
            var stage = GetWithdrawalStage(addiction, now);
            ApplyWithdrawal(uid, addiction, stage, now);
        }
    }

    private NicotineWithdrawalStage GetWithdrawalStage(NicotineAddictionComponent addiction, TimeSpan now)
    {
        if (addiction.WithdrawalSuppressedUntil > now)
            return NicotineWithdrawalStage.None;

        var sinceNicotine = now - addiction.LastNicotineTime;

        if (sinceNicotine >= SevereWithdrawalDelay)
            return NicotineWithdrawalStage.Severe;

        if (sinceNicotine >= MildWithdrawalDelay)
            return NicotineWithdrawalStage.Mild;

        if (sinceNicotine >= CravingDelay)
            return NicotineWithdrawalStage.Craving;

        return NicotineWithdrawalStage.None;
    }

    private void ApplyWithdrawal(EntityUid uid, NicotineAddictionComponent addiction, NicotineWithdrawalStage stage, TimeSpan now)
    {
        if (stage == NicotineWithdrawalStage.None)
        {
            ClearWithdrawal(uid);
            return;
        }

        var popupDelay = stage switch
        {
            NicotineWithdrawalStage.Craving => TimeSpan.FromMinutes(5),
            NicotineWithdrawalStage.Mild => TimeSpan.FromMinutes(3),
            NicotineWithdrawalStage.Severe => TimeSpan.FromMinutes(2),
            _ => TimeSpan.FromMinutes(5)
        };

        if (addiction.NextPopupTime <= now)
        {
            var loc = stage switch
            {
                NicotineWithdrawalStage.Craving => "nicotine-withdrawal-craving",
                NicotineWithdrawalStage.Mild => "nicotine-withdrawal-mild",
                NicotineWithdrawalStage.Severe => "nicotine-withdrawal-severe",
                _ => "nicotine-withdrawal-craving"
            };

            _popup.PopupEntity(Loc.GetString(loc), uid, uid, PopupType.SmallCaution);
            addiction.NextPopupTime = now + popupDelay;
            Dirty(uid, addiction);
        }

        if (stage < NicotineWithdrawalStage.Mild)
        {
            ClearWithdrawal(uid);
            return;
        }

        var speed = stage == NicotineWithdrawalStage.Severe ? 0.97f : 0.98f;
        var duration = TimeSpan.FromSeconds(UpdateInterval + 5);

        if (HasComp<MovementSpeedModifierComponent>(uid))
            _movement.TryUpdateMovementSpeedModDuration(uid, NicotineWithdrawalSpeed, duration, speed, speed);

        if (HasComp<StaminaComponent>(uid))
            _stamina.RefreshStaminaCritThreshold(uid);

        if (stage == NicotineWithdrawalStage.Severe && _random.Prob(0.15f))
            _status.TryUpdateStatusEffectDuration(uid, Drowsiness, TimeSpan.FromSeconds(8));
    }

    private void ClearWithdrawal(EntityUid uid)
    {
        _status.TryRemoveStatusEffect(uid, NicotineWithdrawalSpeed);

        if (HasComp<StaminaComponent>(uid))
            _stamina.RefreshStaminaCritThreshold(uid);
    }

    private void OnRefreshStaminaThreshold(Entity<NicotineAddictionComponent> ent, ref RefreshStaminaCritThresholdEvent args)
    {
        var stage = GetWithdrawalStage(ent.Comp, _timing.CurTime);

        if (stage == NicotineWithdrawalStage.Mild)
            args.ThresholdValue *= 0.95f;
        else if (stage == NicotineWithdrawalStage.Severe)
            args.ThresholdValue *= 0.90f;
    }

    private enum NicotineWithdrawalStage : byte
    {
        None,
        Craving,
        Mild,
        Severe
    }
}
