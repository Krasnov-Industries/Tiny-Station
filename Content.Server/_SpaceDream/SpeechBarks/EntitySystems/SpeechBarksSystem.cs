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

    private const int MaxProsodySegments = 32;

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

        var segments = BuildPlaybackSegments(text, bark, condition);
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

    private SpeechBarkPlaybackSegment[] BuildPlaybackSegments(string text, BarkPrototype bark, BarkCondition condition)
    {
        var cap = Math.Min(Math.Max(0, bark.MaxBarks), _maxBarksPerPhrase);
        if (cap <= 0)
            return [];

        var textSegments = GetTextSegments(text);
        if (textSegments.Count == 0)
            return [];

        var remaining = cap;
        var playbackSegments = new List<SpeechBarkPlaybackSegment>(Math.Min(textSegments.Count, MaxProsodySegments));

        foreach (var textSegment in textSegments)
        {
            if (remaining <= 0)
                break;

            var modifiers = GetProsodyModifiers(textSegment);
            var count = Math.Min(GetBarkCount(textSegment, bark, condition, modifiers), remaining);
            if (count <= 0)
                continue;

            remaining -= count;

            var delayMultiplier = condition.DelayMultiplier * modifiers.DelayMultiplier;
            var minDelay = Math.Clamp(bark.MinDelay * delayMultiplier, 0.025f, 0.75f);
            var maxDelay = Math.Clamp(bark.MaxDelay * delayMultiplier, minDelay, 1f);

            playbackSegments.Add(new SpeechBarkPlaybackSegment(
                Math.Clamp(bark.Pitch * condition.PitchMultiplier * modifiers.PitchMultiplier, 0.1f, 4f),
                Math.Clamp(bark.PitchJitter + condition.PitchJitterBonus + modifiers.PitchJitterBonus, 0f, 1f),
                minDelay,
                maxDelay,
                Math.Clamp(modifiers.PitchRamp, -0.75f, 0.75f),
                count,
                Math.Clamp(condition.VolumeMultiplier * modifiers.VolumeMultiplier, 0f, 3f),
                Math.Clamp(modifiers.PauseAfter * condition.DelayMultiplier, 0f, 0.75f),
                condition.LowpassFilter || modifiers.LowpassFilter));
        }

        return playbackSegments.ToArray();
    }

    private List<BarkTextSegment> GetTextSegments(string text)
    {
        var segments = new List<BarkTextSegment>();
        var builder = new BarkTextSegmentBuilder();

        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];
            if (character == '.' && i + 2 < text.Length && text[i + 1] == '.' && text[i + 2] == '.')
            {
                builder.RegisterEllipsis();
                AddTextSegment(segments, builder);
                i += 2;
                continue;
            }

            builder.Register(character);

            if (!IsSegmentBreak(character))
                continue;

            if (character is '!' or '?')
            {
                while (i + 1 < text.Length && text[i + 1] is '!' or '?')
                {
                    builder.Register(text[++i]);
                }
            }

            AddTextSegment(segments, builder);
        }

        AddTextSegment(segments, builder);
        return segments;
    }

    private void AddTextSegment(List<BarkTextSegment> segments, BarkTextSegmentBuilder builder)
    {
        if (!builder.HasContent)
        {
            builder.Reset();
            return;
        }

        var segment = builder.ToSegment();
        if (segments.Count < MaxProsodySegments)
        {
            segments.Add(segment);
        }
        else
        {
            segments[^1] = MergeSegments(segments[^1], segment);
        }

        builder.Reset();
    }

    private static bool IsSegmentBreak(char character)
    {
        return character is ',' or ';' or ':' or '.' or '!' or '?';
    }

    private static BarkTextSegment MergeSegments(BarkTextSegment left, BarkTextSegment right)
    {
        return new BarkTextSegment(
            left.SpokenCharacters + right.SpokenCharacters,
            left.Punctuation + right.Punctuation,
            left.Letters + right.Letters,
            left.UpperLetters + right.UpperLetters,
            left.Exclamations + right.Exclamations,
            left.Questions + right.Questions,
            left.Commas + right.Commas,
            left.Periods + right.Periods,
            left.Ellipses + right.Ellipses);
    }

    private int GetBarkCount(
        BarkTextSegment segment,
        BarkPrototype bark,
        BarkCondition condition,
        BarkProsodyModifiers modifiers)
    {
        var charsPerBark = Math.Max(1, bark.CharactersPerBark);
        var count = segment.SpokenCharacters / charsPerBark + 1 + segment.Punctuation / 3;
        count = (int) MathF.Ceiling(count * condition.CountMultiplier * modifiers.CountMultiplier);
        return Math.Max(1, count);
    }

    private BarkProsodyModifiers GetProsodyModifiers(BarkTextSegment segment)
    {
        var caps = GetCapsIntensity(segment);
        var exclamation = Math.Clamp(segment.Exclamations * 0.55f, 0f, 1f);
        var question = Math.Clamp(segment.Questions * 0.55f, 0f, 1f);
        var comma = Math.Clamp(segment.Commas * 0.5f, 0f, 1f);
        var ellipsis = Math.Clamp(segment.Ellipses, 0, 1);
        var period = segment.Periods > 0 && ellipsis == 0 ? 1f : 0f;

        var pitchMultiplier = 1f + caps * 0.08f + exclamation * 0.05f + question * 0.04f - ellipsis * 0.05f - period * 0.015f;
        var pitchJitterBonus = caps * 0.035f + exclamation * 0.02f + question * 0.015f + ellipsis * 0.015f;
        var delayMultiplier = 1f - caps * 0.16f - exclamation * 0.08f + comma * 0.08f + period * 0.1f + ellipsis * 0.35f;
        var countMultiplier = 1f + caps * 0.08f + exclamation * 0.08f - ellipsis * 0.18f;
        var volumeMultiplier = 1f + caps * 0.25f + exclamation * 0.18f + question * 0.05f - ellipsis * 0.15f;
        var pitchRamp = question * 0.1f + exclamation * 0.04f - ellipsis * 0.06f - period * 0.03f;
        var pauseAfter = comma * 0.055f + period * 0.075f + question * 0.045f + exclamation * 0.025f + ellipsis * 0.2f;

        return new BarkProsodyModifiers(
            Math.Clamp(pitchMultiplier, 0.7f, 1.35f),
            Math.Clamp(pitchJitterBonus, 0f, 0.25f),
            Math.Clamp(delayMultiplier, 0.65f, 1.55f),
            Math.Clamp(countMultiplier, 0.6f, 1.3f),
            Math.Clamp(volumeMultiplier, 0.45f, 1.6f),
            Math.Clamp(pitchRamp, -0.25f, 0.25f),
            Math.Clamp(pauseAfter, 0f, 0.45f),
            ellipsis > 0f);
    }

    private static float GetCapsIntensity(BarkTextSegment segment)
    {
        if (segment.Letters < 4 || segment.UpperLetters < 4)
            return 0f;

        var ratio = segment.UpperLetters / (float) segment.Letters;
        if (ratio < 0.65f)
            return 0f;

        return Math.Clamp((ratio - 0.65f) / 0.35f, 0f, 1f);
    }

    private BarkCondition GetCondition(EntityUid source)
    {
        if (TryComp<MobStateComponent>(source, out var mobState))
        {
            if (mobState.CurrentState == MobState.Dead)
                return BarkCondition.SilentCondition;

            if (mobState.CurrentState == MobState.Critical)
                return BarkCondition.CriticalCondition;
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
            >= 0.75f => BarkCondition.HeavyDamageCondition,
            >= 0.35f => BarkCondition.LightDamageCondition,
            _ => BarkCondition.HealthyCondition,
        };
    }

    private readonly record struct BarkCondition(
        float PitchMultiplier,
        float PitchJitterBonus,
        float DelayMultiplier,
        float CountMultiplier,
        float VolumeMultiplier,
        bool LowpassFilter,
        bool Silent)
    {
        public static readonly BarkCondition HealthyCondition = new(1f, 0f, 1f, 1f, 1f, false, false);
        public static readonly BarkCondition LightDamageCondition = new(0.95f, 0.025f, 1.12f, 0.95f, 0.86f, false, false);
        public static readonly BarkCondition HeavyDamageCondition = new(0.88f, 0.055f, 1.28f, 0.9f, 0.72f, true, false);
        public static readonly BarkCondition CriticalCondition = new(0.8f, 0.09f, 1.55f, 0.78f, 0.55f, true, false);
        public static readonly BarkCondition SilentCondition = new(1f, 0f, 1f, 0f, 0f, true, true);
    }

    private readonly record struct BarkTextSegment(
        int SpokenCharacters,
        int Punctuation,
        int Letters,
        int UpperLetters,
        int Exclamations,
        int Questions,
        int Commas,
        int Periods,
        int Ellipses);

    private readonly record struct BarkProsodyModifiers(
        float PitchMultiplier,
        float PitchJitterBonus,
        float DelayMultiplier,
        float CountMultiplier,
        float VolumeMultiplier,
        float PitchRamp,
        float PauseAfter,
        bool LowpassFilter);

    private sealed class BarkTextSegmentBuilder
    {
        private int _spokenCharacters;
        private int _punctuation;
        private int _letters;
        private int _upperLetters;
        private int _exclamations;
        private int _questions;
        private int _commas;
        private int _periods;
        private int _ellipses;

        public bool HasContent => _spokenCharacters > 0;

        public void Register(char character)
        {
            if (char.IsWhiteSpace(character))
                return;

            if (char.IsLetter(character))
            {
                _spokenCharacters++;
                _letters++;

                if (char.IsUpper(character))
                    _upperLetters++;

                return;
            }

            if (char.IsDigit(character) || char.IsSymbol(character))
            {
                _spokenCharacters++;
                return;
            }

            RegisterPunctuation(character);
        }

        public void RegisterEllipsis()
        {
            _punctuation += 3;
            _periods += 3;
            _ellipses++;
        }

        public BarkTextSegment ToSegment()
        {
            return new BarkTextSegment(
                _spokenCharacters,
                _punctuation,
                _letters,
                _upperLetters,
                _exclamations,
                _questions,
                _commas,
                _periods,
                _ellipses);
        }

        public void Reset()
        {
            _spokenCharacters = 0;
            _punctuation = 0;
            _letters = 0;
            _upperLetters = 0;
            _exclamations = 0;
            _questions = 0;
            _commas = 0;
            _periods = 0;
            _ellipses = 0;
        }

        private void RegisterPunctuation(char character)
        {
            if (!char.IsPunctuation(character))
                return;

            _punctuation++;

            switch (character)
            {
                case '!':
                    _exclamations++;
                    break;
                case '?':
                    _questions++;
                    break;
                case ',':
                case ';':
                case ':':
                    _commas++;
                    break;
                case '.':
                    _periods++;
                    break;
            }
        }
    }
}
