using Content.Server._Goobstation.SpaceWhale.Threat;
using Content.Shared.Camera;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Jittering;
using Content.Shared.Movement.Systems;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Goobstation.SpaceWhale.AI;

/// <summary>
/// Топорная версия: одна способность — Roar (AoE stun + slow + jitter + shake).
/// </summary>
public sealed partial class WhaleAbilitySystem : EntitySystem
{
    private static readonly SoundSpecifier RoarSound = new SoundPathSpecifier("/Audio/_Goobstation/Ambience/SpaceWhale/whale-roar-1.ogg");

    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedCameraRecoilSystem _camera = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private MovementModStatusSystem _movement = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private WhaleThreatSystem _threat = default!;

    public bool TryRoar(EntityUid whale, float cooldown)
    {
        var ability = EnsureComp<WhaleAbilityComponent>(whale);
        if (ability.NextRoar > _timing.CurTime)
            return false;

        ability.NextRoar = _timing.CurTime + TimeSpan.FromSeconds(cooldown);
        _audio.PlayPvs(RoarSound, whale);

        var hit = 0;
        var origin = Transform(whale).Coordinates;
        foreach (var target in _lookup.GetEntitiesInRange<MobStateComponent>(origin, 8f))
        {
            if (target.Owner == whale)
                continue;

            if (HasComp<WhaleSpawnedByComponent>(target.Owner) || HasComp<SpaceWhaleSegmentComponent>(target.Owner))
                continue;

            if (!_mobState.IsAlive(target.Owner, target.Comp))
                continue;

            _stun.TryUpdateStunDuration(target.Owner, TimeSpan.FromSeconds(2));
            _movement.TryUpdateMovementSpeedModDuration(target.Owner, MovementModStatusSystem.TaserSlowdown, TimeSpan.FromSeconds(5), 0.55f);
            _jitter.DoJitter(target.Owner, TimeSpan.FromSeconds(3), true, 20f, 6f);
            _camera.KickCamera(target.Owner, _random.NextAngle().ToVec() * 0.5f);
            hit++;
        }

        _threat.LogWhale($"Roar fired, hit {hit} targets");
        return true;
    }
}
