using Content.Shared._Tinystation.Nicotine.EntityEffects;
using Content.Server.Popups;
using Content.Shared._Tinystation.Nicotine.Components;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
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
    private const float BloodNicotineIncreaseEpsilon = 0.01f;

    private const string NicotineReagent = "Nicotine";

    private static readonly TimeSpan CravingDelay = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan MildWithdrawalDelay = TimeSpan.FromMinutes(25);
    private static readonly TimeSpan SevereWithdrawalDelay = TimeSpan.FromMinutes(40);
    private static readonly TimeSpan NicotineBuffDuration = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan NicotineStaminaDuration = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan DrowsinessRemoveTime = TimeSpan.FromSeconds(2);

    private float _accumulator;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MetaDataComponent, EntityEffectEvent<NicotineEffect>>(OnNicotineEffect);
        SubscribeLocalEvent<MetaDataComponent, EntityEffectEvent<CytisineEffect>>(OnCytisineEffect);
        SubscribeLocalEvent<NicotineAddictionComponent, ComponentStartup>(OnAddictionStartup);
        SubscribeLocalEvent<NicotineAddictionComponent, RefreshStaminaCritThresholdEvent>(OnRefreshStaminaThreshold);
        SubscribeLocalEvent<BloodstreamComponent, SolutionChangedEvent>(OnBloodstreamSolutionChanged);
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

            ResetWithdrawalTimer(ent.Owner, addiction, now);
            addiction.CureProgress = MathF.Max(0f, addiction.CureProgress - NicotineCurePenalty * args.Scale);
            Dirty(ent.Owner, addiction);

            if (stage >= NicotineWithdrawalStage.Craving)
                _popup.PopupEntity(Loc.GetString("nicotine-addiction-relieved"), ent.Owner, ent.Owner, PopupType.Small);

            return;
        }

        AddExposure(ent.Owner, args.Effect.ExposurePerUnit * args.Scale, now);
    }

    private void OnBloodstreamSolutionChanged(Entity<BloodstreamComponent> ent, ref SolutionChangedEvent args)
    {
        if (args.Solution.Comp.Id != ent.Comp.BloodSolutionName ||
            !TryComp<NicotineAddictionComponent>(ent.Owner, out var addiction))
        {
            return;
        }

        var nicotine = args.Solution.Comp.Solution.GetTotalPrototypeQuantity(NicotineReagent).Float();
        if (nicotine > addiction.LastKnownBloodNicotine + BloodNicotineIncreaseEpsilon)
            ResetWithdrawalTimer(ent.Owner, addiction, _timing.CurTime);

        addiction.LastKnownBloodNicotine = nicotine;
        Dirty(ent.Owner, addiction);
    }

    private void ResetWithdrawalTimer(EntityUid uid, NicotineAddictionComponent addiction, TimeSpan now)
    {
        addiction.LastNicotineTime = now;
        addiction.HasReceivedNicotine = true;
        addiction.WithdrawalSuppressedUntil = now;
        addiction.NextPopupTime = now + TimeSpan.FromMinutes(8);
        ClearWithdrawal(uid);
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
            _movement.TryUpdateMovementSpeedModDuration(uid, NicotineSpeed, NicotineBuffDuration, 1.06f, 1.06f);

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

        var speed = stage == NicotineWithdrawalStage.Severe ? 0.85f : 0.92f;
        var duration = TimeSpan.FromSeconds(UpdateInterval + 5);

        if (HasComp<MovementSpeedModifierComponent>(uid))
            _movement.TryUpdateMovementSpeedModDuration(uid, NicotineWithdrawalSpeed, duration, speed, speed);

        if (HasComp<StaminaComponent>(uid))
            _stamina.RefreshStaminaCritThreshold(uid);

        if (stage == NicotineWithdrawalStage.Severe && _random.Prob(0.30f))
            _status.TryUpdateStatusEffectDuration(uid, Drowsiness, TimeSpan.FromSeconds(12));
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
            args.ThresholdValue *= 0.80f;
        else if (stage == NicotineWithdrawalStage.Severe)
            args.ThresholdValue *= 0.60f;
    }

    public bool TryRunDebugCommand(EntityUid uid, string mode, float amount, out string message)
    {
        var now = _timing.CurTime;

        switch (mode.ToLowerInvariant())
        {
            case "status":
                message = GetDebugStatus(uid, now);
                return true;

            case "clear":
            case "reset":
                RemComp<NicotineAddictionComponent>(uid);
                RemComp<NicotineExposureComponent>(uid);
                ClearWithdrawal(uid);
                message = "Никотиновая зависимость и привыкание очищены.";
                return true;

            case "exposure":
                AddExposure(uid, amount, now);
                message = $"Добавлено привыкание к никотину: {amount:0.##}.";
                return true;

            case "addicted":
            case "none":
                SetDebugStage(uid, now, TimeSpan.Zero);
                message = "Никотиновая зависимость выставлена без ломки.";
                return true;

            case "craving":
                SetDebugStage(uid, now, CravingDelay + TimeSpan.FromMinutes(1));
                message = "Никотиновая ломка выставлена: тяга.";
                return true;

            case "mild":
                SetDebugStage(uid, now, MildWithdrawalDelay + TimeSpan.FromMinutes(1));
                message = "Никотиновая ломка выставлена: средняя.";
                return true;

            case "severe":
                SetDebugStage(uid, now, SevereWithdrawalDelay + TimeSpan.FromMinutes(1));
                message = "Никотиновая ломка выставлена: сильная.";
                return true;

            case "suppress":
                var suppressed = EnsureComp<NicotineAddictionComponent>(uid);
                var suppressMinutes = MathF.Max(1f, amount);
                suppressed.WithdrawalSuppressedUntil = now + TimeSpan.FromMinutes(suppressMinutes);
                suppressed.NextPopupTime = suppressed.WithdrawalSuppressedUntil;
                Dirty(uid, suppressed);
                ClearWithdrawal(uid);
                message = $"Никотиновая ломка подавлена на {suppressMinutes:0.##} мин.";
                return true;

            case "cure":
                var addiction = EnsureComp<NicotineAddictionComponent>(uid);
                addiction.CureProgress += amount;
                if (addiction.CureProgress >= CureThreshold)
                {
                    RemComp<NicotineAddictionComponent>(uid);
                    ClearWithdrawal(uid);
                    message = "Никотиновая зависимость вылечена.";
                    return true;
                }

                Dirty(uid, addiction);
                message = $"Добавлено лечение: {amount:0.##}. Сейчас: {addiction.CureProgress:0.##}/{CureThreshold:0.##}.";
                return true;

            default:
                message = "Неизвестный режим. Используй: status, clear, exposure, addicted, craving, mild, severe, suppress, cure.";
                return false;
        }
    }

    private string GetDebugStatus(EntityUid uid, TimeSpan now)
    {
        if (!TryComp<NicotineAddictionComponent>(uid, out var addiction))
        {
            if (TryComp<NicotineExposureComponent>(uid, out var exposure))
                return $"Зависимости нет. Привыкание: {exposure.Exposure:0.##}/{AddictionThreshold:0.##}.";

            return "Зависимости нет. Привыкания к никотину нет.";
        }

        var stage = GetWithdrawalStage(addiction, now);
        var sinceNicotine = now - addiction.LastNicotineTime;
        var nextStage = GetNextWithdrawalStage(stage);
        var nextStageText = nextStage is null
            ? "Следующей стадии нет."
            : $"Следующая стадия: {FormatStage(nextStage.Value)} через {FormatTime(GetTimeUntilNextStage(addiction, now, nextStage.Value))}.";

        var suppressedText = addiction.WithdrawalSuppressedUntil > now
            ? $" Ломка подавлена ещё {FormatTime(addiction.WithdrawalSuppressedUntil - now)}."
            : string.Empty;

        return $"Стадия: {FormatStage(stage)}. Без никотина: {FormatTime(sinceNicotine)}. {nextStageText}{suppressedText} Лечение: {addiction.CureProgress:0.##}/{CureThreshold:0.##}.";
    }

    private static string FormatStage(NicotineWithdrawalStage stage)
    {
        return stage switch
        {
            NicotineWithdrawalStage.None => "нет ломки",
            NicotineWithdrawalStage.Craving => "тяга",
            NicotineWithdrawalStage.Mild => "средняя ломка",
            NicotineWithdrawalStage.Severe => "сильная ломка",
            _ => stage.ToString()
        };
    }

    private static NicotineWithdrawalStage? GetNextWithdrawalStage(NicotineWithdrawalStage stage)
    {
        return stage switch
        {
            NicotineWithdrawalStage.None => NicotineWithdrawalStage.Craving,
            NicotineWithdrawalStage.Craving => NicotineWithdrawalStage.Mild,
            NicotineWithdrawalStage.Mild => NicotineWithdrawalStage.Severe,
            _ => null
        };
    }

    private static TimeSpan GetTimeUntilNextStage(NicotineAddictionComponent addiction, TimeSpan now, NicotineWithdrawalStage nextStage)
    {
        var delay = nextStage switch
        {
            NicotineWithdrawalStage.Craving => CravingDelay,
            NicotineWithdrawalStage.Mild => MildWithdrawalDelay,
            NicotineWithdrawalStage.Severe => SevereWithdrawalDelay,
            _ => TimeSpan.Zero
        };

        var remaining = addiction.LastNicotineTime + delay - now;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private static string FormatTime(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
            time = TimeSpan.Zero;

        if (time.TotalHours >= 1)
            return $"{(int) time.TotalHours}h {time.Minutes}m {time.Seconds}s";

        if (time.TotalMinutes >= 1)
            return $"{time.Minutes}m {time.Seconds}s";

        return $"{time.Seconds}s";
    }

    private void SetDebugStage(EntityUid uid, TimeSpan now, TimeSpan sinceNicotine)
    {
        RemComp<NicotineExposureComponent>(uid);

        var addiction = EnsureComp<NicotineAddictionComponent>(uid);
        addiction.LastNicotineTime = now - sinceNicotine;
        addiction.HasReceivedNicotine = true;
        addiction.WithdrawalSuppressedUntil = TimeSpan.Zero;
        addiction.NextPopupTime = now;
        Dirty(uid, addiction);
    }

    private enum NicotineWithdrawalStage : byte
    {
        None,
        Craving,
        Mild,
        Severe
    }
}
