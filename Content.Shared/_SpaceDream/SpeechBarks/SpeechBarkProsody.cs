using Content.Shared._SpaceDream.SpeechBarks.Events;
using Content.Shared._SpaceDream.SpeechBarks.Prototypes;

namespace Content.Shared._SpaceDream.SpeechBarks;

public static class SpeechBarkProsody
{
    public const int MaxProsodySegments = 48;
    public const string PreviewText = "Привет! ЭЙ, КАК ДЕЛА?.. нормально?";

    public static SpeechBarkPlaybackSegment[] BuildPlaybackSegments(
        string text,
        BarkPrototype bark,
        int maxBarks,
        SpeechBarkCondition condition)
    {
        var cap = Math.Min(Math.Max(0, bark.MaxBarks), Math.Max(0, maxBarks));
        if (cap <= 0 || condition.Silent)
            return [];

        var textSegments = GetTextSegments(text);
        if (textSegments.Count == 0)
            return [];

        var builds = new List<SegmentBuildData>(Math.Min(textSegments.Count, MaxProsodySegments));
        var desiredTotal = 0f;

        foreach (var textSegment in textSegments)
        {
            var modifiers = GetProsodyModifiers(textSegment);
            var desiredCount = GetBarkCount(textSegment, bark, condition, modifiers);
            if (desiredCount <= 0)
                continue;

            builds.Add(new SegmentBuildData(textSegment, modifiers, desiredCount));
            desiredTotal += desiredCount;
        }

        if (builds.Count == 0 || desiredTotal <= 0f)
            return [];

        var remainingCap = cap;
        var remainingDesired = desiredTotal;
        var playbackSegments = new List<SpeechBarkPlaybackSegment>(builds.Count);

        foreach (var build in builds)
        {
            if (remainingCap <= 0 || remainingDesired <= 0f)
                break;

            var share = build.DesiredCount / remainingDesired * remainingCap;
            var count = Math.Clamp((int) MathF.Round(share), 1, remainingCap);
            remainingCap -= count;
            remainingDesired -= build.DesiredCount;

            var modifiers = build.Modifiers;
            var delayMultiplier = condition.DelayMultiplier * modifiers.DelayMultiplier;
            var minDelay = Math.Clamp(bark.MinDelay * delayMultiplier, 0.025f, 0.75f);
            var maxDelay = Math.Clamp(bark.MaxDelay * delayMultiplier, minDelay, 1f);

            playbackSegments.Add(new SpeechBarkPlaybackSegment(
                Math.Clamp(bark.Pitch * condition.PitchMultiplier * modifiers.PitchMultiplier, 0.1f, 4f),
                Math.Clamp(bark.PitchJitter + condition.PitchJitterBonus + modifiers.PitchJitterBonus, 0f, 1f),
                minDelay,
                maxDelay,
                Math.Clamp(modifiers.PitchRamp, -4f, 4f),
                count,
                Math.Clamp(condition.VolumeMultiplier * modifiers.VolumeMultiplier, 0f, 3f),
                Math.Clamp(modifiers.PauseAfter * condition.DelayMultiplier, 0f, 0.75f),
                condition.LowpassFilter || modifiers.LowpassFilter,
                modifiers.PitchStepStyle));
        }

        return playbackSegments.ToArray();
    }

    private static List<BarkTextSegment> GetTextSegments(string text)
    {
        var segments = new List<BarkTextSegment>();
        var builder = new BarkTextSegmentBuilder();

        for (var i = 0; i < text.Length;)
        {
            var character = text[i];
            if (char.IsWhiteSpace(character))
            {
                i++;
                continue;
            }

            if (IsEllipsis(text, i))
            {
                builder.RegisterEllipsis();
                AddTextSegment(segments, builder);
                i += 3;
                continue;
            }

            if (IsSegmentBreak(character))
            {
                builder.Register(character);

                if (character is '!' or '?')
                {
                    while (i + 1 < text.Length && text[i + 1] is '!' or '?')
                        builder.Register(text[++i]);
                }

                AddTextSegment(segments, builder);
                i++;
                continue;
            }

            var start = i;
            while (i < text.Length &&
                   !char.IsWhiteSpace(text[i]) &&
                   !IsSegmentBreak(text[i]) &&
                   !IsEllipsis(text, i))
            {
                i++;
            }

            var token = text[start..i];
            var style = GetTokenStyle(token);
            if (builder.HasContent && ShouldSplitForStyle(builder.Style, style))
                AddTextSegment(segments, builder);

            builder.RegisterToken(token, style);
        }

        AddTextSegment(segments, builder);
        return segments;
    }

    private static void AddTextSegment(List<BarkTextSegment> segments, BarkTextSegmentBuilder builder)
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

    private static bool IsEllipsis(string text, int index)
    {
        return index + 2 < text.Length &&
               text[index] == '.' &&
               text[index + 1] == '.' &&
               text[index + 2] == '.';
    }

    private static BarkTokenStyle GetTokenStyle(string token)
    {
        var letters = 0;
        var upperLetters = 0;

        foreach (var character in token)
        {
            if (!char.IsLetter(character))
                continue;

            letters++;
            if (char.IsUpper(character))
                upperLetters++;
        }

        if (letters < 2 || upperLetters < 2)
            return BarkTokenStyle.Normal;

        return upperLetters / (float) letters >= 0.7f
            ? BarkTokenStyle.Caps
            : BarkTokenStyle.Normal;
    }

    private static bool ShouldSplitForStyle(BarkTokenStyle current, BarkTokenStyle next)
    {
        if (current == BarkTokenStyle.Neutral || next == BarkTokenStyle.Neutral)
            return false;

        return current != next && (current == BarkTokenStyle.Caps || next == BarkTokenStyle.Caps);
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
            left.Ellipses + right.Ellipses,
            left.CapsWords + right.CapsWords,
            left.NormalWords + right.NormalWords,
            left.RepeatedLetters + right.RepeatedLetters,
            left.Style == right.Style ? left.Style : BarkTokenStyle.Normal);
    }

    private static int GetBarkCount(
        BarkTextSegment segment,
        BarkPrototype bark,
        SpeechBarkCondition condition,
        BarkProsodyModifiers modifiers)
    {
        var charsPerBark = Math.Max(1, bark.CharactersPerBark);
        var count = segment.SpokenCharacters / charsPerBark + 1 + segment.Punctuation / 3;
        count = (int) MathF.Ceiling(count * condition.CountMultiplier * modifiers.CountMultiplier);
        return Math.Max(1, count);
    }

    private static BarkProsodyModifiers GetProsodyModifiers(BarkTextSegment segment)
    {
        var caps = GetCapsIntensity(segment);
        var exclamation = Math.Clamp(segment.Exclamations * 0.55f, 0f, 1f);
        var question = Math.Clamp(segment.Questions * 0.55f, 0f, 1f);
        var comma = Math.Clamp(segment.Commas * 0.5f, 0f, 1f);
        var ellipsis = Math.Clamp(segment.Ellipses, 0, 1);
        var period = segment.Periods > 0 && ellipsis == 0 ? 1f : 0f;
        var stretch = Math.Clamp(segment.RepeatedLetters * 0.16f, 0f, 1f);

        var pitchMultiplier = 1f + caps * 0.13f + exclamation * 0.07f + question * 0.04f - ellipsis * 0.08f - period * 0.02f - stretch * 0.02f;
        var pitchJitterBonus = caps * 0.025f + exclamation * 0.018f + question * 0.012f + ellipsis * 0.01f + stretch * 0.025f;
        var delayMultiplier = 1f - caps * 0.22f - exclamation * 0.12f + comma * 0.1f + period * 0.11f + ellipsis * 0.42f + stretch * 0.08f;
        var countMultiplier = 1f + caps * 0.16f + exclamation * 0.12f + stretch * 0.18f - ellipsis * 0.16f;
        var volumeMultiplier = 1f + caps * 0.35f + exclamation * 0.22f + question * 0.06f - ellipsis * 0.18f;
        var pitchRamp = question * 2.4f + exclamation * 0.8f + stretch * 0.6f - ellipsis * 1.2f - period * 0.6f;
        var pauseAfter = comma * 0.06f + period * 0.09f + question * 0.055f + exclamation * 0.03f + ellipsis * 0.24f;
        var pitchStepStyle = GetPitchStepStyle(caps, exclamation, question, ellipsis, stretch);

        return new BarkProsodyModifiers(
            Math.Clamp(pitchMultiplier, 0.7f, 1.35f),
            Math.Clamp(pitchJitterBonus, 0f, 0.25f),
            Math.Clamp(delayMultiplier, 0.58f, 1.65f),
            Math.Clamp(countMultiplier, 0.6f, 1.45f),
            Math.Clamp(volumeMultiplier, 0.45f, 1.75f),
            Math.Clamp(pitchRamp, -4f, 4f),
            Math.Clamp(pauseAfter, 0f, 0.45f),
            ellipsis > 0f,
            pitchStepStyle);
    }

    private static float GetCapsIntensity(BarkTextSegment segment)
    {
        if (segment.CapsWords <= 0 || segment.Letters < 2)
            return 0f;

        var ratio = segment.UpperLetters / (float) segment.Letters;
        return Math.Clamp(0.35f + ratio * 0.8f, 0f, 1f);
    }

    private static SpeechBarkPitchStepStyle GetPitchStepStyle(
        float caps,
        float exclamation,
        float question,
        float ellipsis,
        float stretch)
    {
        if (ellipsis > 0f)
            return SpeechBarkPitchStepStyle.Tired;

        if (question > 0.15f && exclamation > 0.15f)
            return SpeechBarkPitchStepStyle.Unstable;

        if (stretch > 0.45f)
            return SpeechBarkPitchStepStyle.Unstable;

        if (question > 0.15f)
            return SpeechBarkPitchStepStyle.Question;

        if (caps > 0.25f || exclamation > 0.15f)
            return SpeechBarkPitchStepStyle.Emphatic;

        return SpeechBarkPitchStepStyle.Neutral;
    }

    private static int CountRepeatedLetters(string token)
    {
        var repeated = 0;
        var runLength = 0;
        var previous = '\0';

        foreach (var character in token)
        {
            if (!char.IsLetter(character))
            {
                runLength = 0;
                previous = '\0';
                continue;
            }

            var lower = char.ToLowerInvariant(character);
            if (lower == previous)
            {
                runLength++;
                if (runLength >= 3)
                    repeated++;
            }
            else
            {
                previous = lower;
                runLength = 1;
            }
        }

        return repeated;
    }

    private readonly record struct SegmentBuildData(
        BarkTextSegment Segment,
        BarkProsodyModifiers Modifiers,
        int DesiredCount);

    private readonly record struct BarkTextSegment(
        int SpokenCharacters,
        int Punctuation,
        int Letters,
        int UpperLetters,
        int Exclamations,
        int Questions,
        int Commas,
        int Periods,
        int Ellipses,
        int CapsWords,
        int NormalWords,
        int RepeatedLetters,
        BarkTokenStyle Style);

    private readonly record struct BarkProsodyModifiers(
        float PitchMultiplier,
        float PitchJitterBonus,
        float DelayMultiplier,
        float CountMultiplier,
        float VolumeMultiplier,
        float PitchRamp,
        float PauseAfter,
        bool LowpassFilter,
        SpeechBarkPitchStepStyle PitchStepStyle);

    private enum BarkTokenStyle : byte
    {
        Neutral,
        Normal,
        Caps,
    }

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
        private int _capsWords;
        private int _normalWords;
        private int _repeatedLetters;

        public bool HasContent => _spokenCharacters > 0;
        public BarkTokenStyle Style { get; private set; }

        public void RegisterToken(string token, BarkTokenStyle style)
        {
            if (Style == BarkTokenStyle.Neutral)
                Style = style;

            if (style == BarkTokenStyle.Caps)
                _capsWords++;
            else if (style == BarkTokenStyle.Normal)
                _normalWords++;

            _repeatedLetters += CountRepeatedLetters(token);

            foreach (var character in token)
                Register(character);
        }

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
                _ellipses,
                _capsWords,
                _normalWords,
                _repeatedLetters,
                Style);
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
            _capsWords = 0;
            _normalWords = 0;
            _repeatedLetters = 0;
            Style = BarkTokenStyle.Neutral;
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

public readonly record struct SpeechBarkCondition(
    float PitchMultiplier,
    float PitchJitterBonus,
    float DelayMultiplier,
    float CountMultiplier,
    float VolumeMultiplier,
    bool LowpassFilter,
    bool Silent)
{
    public static readonly SpeechBarkCondition HealthyCondition = new(1f, 0f, 1f, 1f, 1f, false, false);
    public static readonly SpeechBarkCondition LightDamageCondition = new(0.95f, 0.025f, 1.12f, 0.95f, 0.86f, false, false);
    public static readonly SpeechBarkCondition HeavyDamageCondition = new(0.88f, 0.055f, 1.28f, 0.9f, 0.72f, true, false);
    public static readonly SpeechBarkCondition CriticalCondition = new(0.8f, 0.09f, 1.55f, 0.78f, 0.55f, true, false);
    public static readonly SpeechBarkCondition SilentCondition = new(1f, 0f, 1f, 0f, 0f, true, true);
}
