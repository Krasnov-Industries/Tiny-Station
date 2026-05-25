using Content.Shared._SpaceDream.SpeechBarks;
using Content.Shared._SpaceDream.SpeechBarks.Events;
using Content.Shared._SpaceDream.SpeechBarks.Prototypes;
using Robust.Client.Audio;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._SpaceDream.SpeechBarks.Systems;

public sealed partial class SpeechBarksSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const float BaseVolume = -3f;
    private const float PreviewVolumeBoost = 3f;
    private const float WhisperVolumeFade = 7f;
    private const float LowpassVolumeFade = 2f;
    private static readonly TimeSpan FinishedBarkLifetime = TimeSpan.FromSeconds(0.8);

    private readonly List<ActiveBark> _activeBarks = new();
    private bool _enabled;
    private float _volume;
    private int _maxActiveStreams;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<PlaySpeechBarksEvent>(OnPlaySpeechBarks);
        SubscribeNetworkEvent<InterruptSpeechBarksEvent>(OnInterruptSpeechBarks);

        _cfg.OnValueChanged(SpeechBarkCCVars.ClientEnabled, value => _enabled = value, true);
        _cfg.OnValueChanged(SpeechBarkCCVars.Volume, value => _volume = Math.Clamp(value, 0f, 3f), true);
        _cfg.OnValueChanged(SpeechBarkCCVars.MaxActiveStreams, value => _maxActiveStreams = Math.Max(0, value), true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        for (var i = _activeBarks.Count - 1; i >= 0; i--)
        {
            var bark = _activeBarks[i];
            if (!bark.Preview && (!_enabled || _volume <= 0f))
            {
                StopActiveBark(i);
                continue;
            }

            if (bark.Source != null && TerminatingOrDeleted(bark.Source.Value))
            {
                StopActiveBark(i);
                continue;
            }

            if (bark.NextSound > _timing.CurTime)
                continue;

            if (bark.SegmentIndex >= bark.Segments.Length)
            {
                bark.FinishedAt ??= _timing.CurTime;

                if (_timing.CurTime - bark.FinishedAt > FinishedBarkLifetime)
                    _activeBarks.RemoveAt(i);

                continue;
            }

            var segment = bark.Segments[bark.SegmentIndex];
            if (bark.Played >= segment.Count)
            {
                bark.SegmentIndex++;
                bark.Played = 0;

                if (bark.SegmentIndex >= bark.Segments.Length)
                {
                    bark.FinishedAt ??= _timing.CurTime;
                    continue;
                }

                bark.NextSound = _timing.CurTime + TimeSpan.FromSeconds(segment.PauseAfter);
                continue;
            }

            PlayBark(bark, segment);
            bark.Played++;
            bark.NextSound = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(segment.MinDelay, segment.MaxDelay));
        }
    }

    public void PlayLocalPreview(BarkPrototype bark)
    {
        StopPreviewBarks();
        var segment = new SpeechBarkPlaybackSegment(
            Math.Clamp(bark.Pitch, 0.1f, 4f),
            Math.Clamp(bark.PitchJitter, 0f, 1f),
            Math.Clamp(bark.MinDelay, 0.025f, 0.75f),
            Math.Clamp(bark.MaxDelay, bark.MinDelay, 1f),
            0f,
            10,
            1f,
            0f,
            false);

        StartBark(null, bark.Sound, [segment], false, 0f, true, true);
    }

    private void OnPlaySpeechBarks(PlaySpeechBarksEvent ev)
    {
        if (!_enabled || _volume <= 0f || ev.Segments.Length == 0)
            return;

        var source = GetEntity(ev.Source);
        if (Transform(source).MapID == MapId.Nullspace)
            return;

        StopBarksFor(source);
        StartBark(source, ev.Sound, ev.Segments, ev.IsWhisper, ev.MaxDistance, false, false);
    }

    private void OnInterruptSpeechBarks(InterruptSpeechBarksEvent ev)
    {
        var source = GetEntity(ev.Source);
        StopBarksFor(source);
    }

    private void StartBark(
        EntityUid? source,
        SoundSpecifier sound,
        SpeechBarkPlaybackSegment[] segments,
        bool isWhisper,
        float maxDistance,
        bool global,
        bool preview)
    {
        if (!preview && _maxActiveStreams == 0)
            return;

        var streamLimit = preview ? Math.Max(1, _maxActiveStreams) : _maxActiveStreams;
        while (_activeBarks.Count >= streamLimit)
            StopActiveBark(0);

        var sanitizedSegments = SanitizeSegments(segments);
        if (sanitizedSegments.Length == 0)
            return;

        var active = new ActiveBark
        {
            Source = source,
            Sound = sound,
            Segments = sanitizedSegments,
            IsWhisper = isWhisper,
            MaxDistance = maxDistance,
            Global = global,
            Preview = preview,
        };

        if (preview)
        {
            var segment = active.Segments[0];
            PlayBark(active, segment);
            active.Played = 1;
            active.NextSound = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(segment.MinDelay, segment.MaxDelay));
        }

        _activeBarks.Add(active);
    }

    private SpeechBarkPlaybackSegment[] SanitizeSegments(SpeechBarkPlaybackSegment[] segments)
    {
        var sanitized = new SpeechBarkPlaybackSegment[segments.Length];
        var count = 0;

        foreach (var segment in segments)
        {
            if (segment.Count <= 0)
                continue;

            var minDelay = Math.Clamp(segment.MinDelay, 0.025f, 0.75f);
            var maxDelay = Math.Clamp(segment.MaxDelay, minDelay, 1f);

            sanitized[count++] = new SpeechBarkPlaybackSegment(
                Math.Clamp(segment.Pitch, 0.1f, 4f),
                Math.Clamp(segment.PitchJitter, 0f, 1f),
                minDelay,
                maxDelay,
                Math.Clamp(segment.PitchRamp, -0.75f, 0.75f),
                segment.Count,
                Math.Clamp(segment.VolumeMultiplier, 0f, 3f),
                Math.Clamp(segment.PauseAfter, 0f, 0.75f),
                segment.LowpassFilter);
        }

        if (count == sanitized.Length)
            return sanitized;

        return sanitized[..count];
    }

    private void PlayBark(ActiveBark bark, SpeechBarkPlaybackSegment segment)
    {
        var pitch = _random.NextFloat(segment.Pitch - segment.PitchJitter, segment.Pitch + segment.PitchJitter);
        if (segment.PitchRamp != 0f)
        {
            var progress = segment.Count <= 1 ? 1f : bark.Played / (float) (segment.Count - 1);
            pitch += segment.PitchRamp * progress;
        }

        var volumeGain = (bark.Preview ? Math.Max(_volume, 1f) : _volume) * segment.VolumeMultiplier;
        var volume = BaseVolume + SharedAudioSystem.GainToVolume(Math.Max(volumeGain, 0.001f));

        if (bark.Preview)
            volume += PreviewVolumeBoost;

        if (bark.IsWhisper)
            volume -= WhisperVolumeFade;

        if (segment.LowpassFilter)
        {
            pitch *= 0.92f;
            volume -= LowpassVolumeFade;
        }

        var audioParams = AudioParams.Default
            .WithPitchScale(Math.Clamp(pitch, 0.1f, 4f))
            .WithVolume(volume)
            .WithMaxDistance(bark.Global ? 0f : Math.Max(1f, bark.MaxDistance));

        var resolved = _audio.ResolveSound(bark.Sound);
        (EntityUid Entity, Robust.Shared.Audio.Components.AudioComponent Component)? stream;

        if (bark.Global || bark.Source == null || bark.Source == _player.LocalEntity)
        {
            stream = _audio.PlayGlobal(resolved, Filter.Local(), false, audioParams);
        }
        else
        {
            stream = _audio.PlayEntity(resolved, Filter.Local(), bark.Source.Value, false, audioParams);
        }

        if (stream != null)
            bark.Streams.Add(stream.Value.Entity);
    }

    private void StopBarksFor(EntityUid source)
    {
        for (var i = _activeBarks.Count - 1; i >= 0; i--)
        {
            if (_activeBarks[i].Source == source)
                StopActiveBark(i);
        }
    }

    private void StopPreviewBarks()
    {
        for (var i = _activeBarks.Count - 1; i >= 0; i--)
        {
            if (_activeBarks[i].Preview)
                StopActiveBark(i);
        }
    }

    private void StopActiveBark(int index)
    {
        var bark = _activeBarks[index];
        foreach (var stream in bark.Streams)
            _audio.Stop(stream);

        _activeBarks.RemoveAt(index);
    }

    private sealed class ActiveBark
    {
        public EntityUid? Source;
        public SoundSpecifier Sound = default!;
        public SpeechBarkPlaybackSegment[] Segments = [];
        public int SegmentIndex;
        public bool IsWhisper;
        public float MaxDistance;
        public bool Global;
        public bool Preview;
        public TimeSpan NextSound;
        public TimeSpan? FinishedAt;
        public int Played;
        public List<EntityUid> Streams = new();
    }
}
