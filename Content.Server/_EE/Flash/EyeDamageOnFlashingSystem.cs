using Content.Server._EE.Flash.Components;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Flash;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._EE.Flash;

public sealed partial class EyeDamageOnFlashingSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private BlurryVisionSystem _blurry = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EyeDamageOnFlashingComponent, FlashAttemptEvent>(OnFlashAttempt);
        SubscribeLocalEvent<EyeDamageOnFlashingComponent, AfterFlashedEvent>(OnAfterFlashed);
        SubscribeLocalEvent<EyeDamageOnFlashingComponent, GetBlurEvent>(OnGetBlur);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<EyeDamageOnFlashingComponent, ActiveEyeDamageOnFlashingComponent>();
        while (query.MoveNext(out var uid, out var eyeDamage, out _))
        {
            if (eyeDamage.CurrentBlur <= 0f)
            {
                RemCompDeferred<ActiveEyeDamageOnFlashingComponent>(uid);
                continue;
            }

            if (now < eyeDamage.BlurDecayStartsAt)
                continue;

            var previous = eyeDamage.CurrentBlur;
            eyeDamage.CurrentBlur = MathF.Max(0f, eyeDamage.CurrentBlur - eyeDamage.BlurDecayPerSecond * frameTime);

            if (MathF.Abs(previous - eyeDamage.CurrentBlur) < 0.001f)
                continue;

            _blurry.UpdateBlurMagnitude((uid, null));

            if (eyeDamage.CurrentBlur <= 0f)
                RemCompDeferred<ActiveEyeDamageOnFlashingComponent>(uid);
        }
    }

    private void OnFlashAttempt(Entity<EyeDamageOnFlashingComponent> ent, ref FlashAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        args.DurationMultiplier *= ent.Comp.FlashDurationMultiplier;
    }

    private void OnAfterFlashed(Entity<EyeDamageOnFlashingComponent> ent, ref AfterFlashedEvent args)
    {
        if (!_random.Prob(Math.Clamp(ent.Comp.BlurChance, 0f, 1f)))
            return;

        ent.Comp.CurrentBlur = Math.Clamp(
            ent.Comp.CurrentBlur + ent.Comp.BlurAmount,
            0f,
            Math.Min(ent.Comp.MaxBlur, BlurryVisionComponent.MaxMagnitude));
        ent.Comp.BlurDecayStartsAt = _timing.CurTime + TimeSpan.FromSeconds(MathF.Max(0f, ent.Comp.BlurDecayDelay));

        EnsureComp<ActiveEyeDamageOnFlashingComponent>(ent);
        _blurry.UpdateBlurMagnitude((ent.Owner, null));
    }

    private void OnGetBlur(Entity<EyeDamageOnFlashingComponent> ent, ref GetBlurEvent args)
    {
        args.Blur += ent.Comp.CurrentBlur;
    }
}
