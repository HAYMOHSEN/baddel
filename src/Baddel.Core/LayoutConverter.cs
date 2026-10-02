using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Baddel.Core;

/// <summary>Which way a piece of text is converted.</summary>
public enum ConversionDirection
{
    None = 0,
    /// <summary>Typed on a Latin layout but meant to be Arabic.</summary>
    LatinToArabic = 1,
    /// <summary>Typed on the Arabic layout but meant to be Latin (English).</summary>
    ArabicToLatin = 2,
}

public sealed record ConversionOptions
{
    public static ConversionOptions Default { get; } = new();

    /// <summary>Forces a direction; null detects it from the text.</summary>
    public ConversionDirection? ForcedDirection { get; init; }

    /// <summary>Used when the text holds as many Arabic as Latin letters.</summary>
    public ConversionDirection DirectionWhenTied { get; init; } = ConversionDirection.LatinToArabic;

    /// <summary>Undo the effect of Caps Lock on text typed on a Latin layout.</summary>
    public bool FixCapsLock { get; init; } = true;

    /// <summary>Undo the capital letter Word and Outlook add at the start of a sentence.</summary>
    public bool FixAutoCapitalization { get; init; }

    /// <summary>Whether the text begins a sentence (used by <see cref="FixAutoCapitalization"/>).</summary>
    public bool StartsAtSentenceStart { get; init; } = true;
}

public sealed record ConversionResult(string Original, string Text, ConversionDirection Direction)
{
    public bool Changed => Direction != ConversionDirection.None && !string.Equals(Original, Text, StringComparison.Ordinal);
}

/// <summary>Converts text typed on the wrong keyboard layout into what the user meant to type.</summary>
public sealed class LayoutConverter
{
    private const int MaxAmbiguousPerWord = 6;
    private const string AlefWithHamzaAbove = "\u0623";
    private const string AlefWithHamzaBelow = "\u0625";
    private const string AlefWithMadda = "\u0622";
    private const string Lam = "\u0644";

    private readonly LayoutMap _map;
    private readonly EnglishLexicon _lexicon;

    public LayoutConverter(LayoutMap map, EnglishLexicon? lexicon = null)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _lexicon = lexicon ?? EnglishLexicon.Shared;
    }

    public LayoutMap Map => _map;

    /// <summary>Converts the whole text.</summary>
    public ConversionResult Convert(string text, ConversionOptions? options = null)
    {
        options ??= ConversionOptions.Default;
        if (string.IsNullOrEmpty(text))
            return new ConversionResult(text ?? string.Empty, text ?? string.Empty, ConversionDirection.None);

        string source = TextScripts.NormalizeArabicPresentationForms(text);
        ConversionDirection direction = options.ForcedDirection ?? DetectDirection(source, options.DirectionWhenTied);
        string output;
        switch (direction)
        {
            case ConversionDirection.LatinToArabic:
                output = ConvertLatinToArabic(source, options);
                break;
            case ConversionDirection.ArabicToLatin:
                output = ResolveEnglish(Tokenize(source, _map.ArabicToLatin, _map.MaxArabicKeyLength, allowAlternatives: true));
                break;
            default:
                return new ConversionResult(text, text, ConversionDirection.None);
        }
        return new ConversionResult(text, output, direction);
    }

    /// <summary>
    /// Converts only the most recent run of words written in the wrong script, leaving the rest of the
    /// line untouched. Used when the user pressed the shortcut without selecting anything.
    /// </summary>
    public ConversionResult ConvertLatestRun(string text, ConversionOptions? options = null)
    {
        options ??= ConversionOptions.Default;
        if (string.IsNullOrEmpty(text))
            return new ConversionResult(text ?? string.Empty, text ?? string.Empty, ConversionDirection.None);

        string source = TextScripts.NormalizeArabicPresentationForms(text);
        List<Word> words = SplitWords(source);
        int last = -1;
        for (int k = words.Count - 1; k >= 0; k--)
        {
            if (words[k].Kind != WordKind.Neutral) { last = k; break; }
        }
        if (last < 0) return new ConversionResult(text, text, ConversionDirection.None);

        WordKind kind = words[last].Kind;
        int first = last;
        for (int k = last - 1; k >= 0; k--)
        {
            if (words[k].Kind == kind) first = k;
            else if (words[k].Kind != WordKind.Neutral) break;
        }

        int start = words[first].Start;
        string prefix = source.Substring(0, start);
        ConversionOptions tailOptions = options with
        {
            ForcedDirection = kind == WordKind.Latin ? ConversionDirection.LatinToArabic : ConversionDirection.ArabicToLatin,
            StartsAtSentenceStart = IsSentenceStart(prefix),
        };
        ConversionResult tail = Convert(source.Substring(start), tailOptions);
        return new ConversionResult(text, prefix + tail.Text, tail.Direction);
    }

    /// <summary>Decides which way the text should be converted.</summary>
    public ConversionDirection DetectDirection(string text, ConversionDirection whenTied = ConversionDirection.LatinToArabic)
    {
        int arabic = 0, latin = 0;
        foreach (char c in text)
        {
            if (TextScripts.IsArabicLetter(c)) arabic++;
            else if (TextScripts.IsBasicLatinLetter(c)) latin++;
        }
        if (latin > arabic) return ConversionDirection.LatinToArabic;
        if (arabic > latin) return ConversionDirection.ArabicToLatin;
        if (latin == 0)
        {
            // No letters at all: look for symbols that are letters on the other layout (";" is "ك").
            foreach (char c in text)
            {
                if (_map.LatinToArabic.TryGetValue(c.ToString(), out string? arabicText) && TextScripts.ContainsArabicLetter(arabicText))
                    return ConversionDirection.LatinToArabic;
            }
            foreach (char c in text)
            {
                string key = c.ToString();
                if (_map.ArabicToLatin.TryGetValue(key, out string? latinText) && !string.Equals(latinText, key, StringComparison.Ordinal))
                    return ConversionDirection.ArabicToLatin;
            }
            return ConversionDirection.None;
        }
        return whenTied == ConversionDirection.None ? ConversionDirection.LatinToArabic : whenTied;
    }

    // ---------------------------------------------------------------- Latin -> Arabic

    private string ConvertLatinToArabic(string source, ConversionOptions options)
    {
        char[] chars = source.ToCharArray();
        bool capsLockFixed = options.FixCapsLock && FixCapsLock(chars);
        if (!capsLockFixed && options.FixAutoCapitalization)
            FixSentenceStartCapitals(chars, options.StartsAtSentenceStart);

        var output = new StringBuilder(source.Length + 8);
        foreach (Segment segment in Tokenize(new string(chars), _map.LatinToArabic, _map.MaxLatinKeyLength, allowAlternatives: false))
            output.Append(segment.Primary);
        return output.ToString();
    }

    /// <summary>Mostly-capital text means Caps Lock was on: swap the case back.</summary>
    private static bool FixCapsLock(char[] chars)
    {
        int upper = 0, lower = 0;
        foreach (char c in chars)
        {
            if (c is >= 'A' and <= 'Z') upper++;
            else if (c is >= 'a' and <= 'z') lower++;
        }
        if (upper < 3 || lower * 4 > upper) return false;
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            if (c is >= 'A' and <= 'Z') chars[i] = (char)(c + 32);
            else if (c is >= 'a' and <= 'z') chars[i] = (char)(c - 32);
        }
        return true;
    }

    /// <summary>Removes the capital letter Office adds at the start of each sentence.</summary>
    private void FixSentenceStartCapitals(char[] chars, bool startsAtSentenceStart)
    {
        bool atSentenceStart = startsAtSentenceStart;
        bool pendingSentenceEnd = false;
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            if (c is '\r' or '\n')
            {
                atSentenceStart = true;
                pendingSentenceEnd = false;
                continue;
            }
            if (char.IsWhiteSpace(c))
            {
                if (pendingSentenceEnd)
                {
                    atSentenceStart = true;
                    pendingSentenceEnd = false;
                }
                continue;
            }
            if (atSentenceStart)
            {
                atSentenceStart = false;
                int end = i;
                while (end < chars.Length && !char.IsWhiteSpace(chars[end])) end++;
                if (c is >= 'A' and <= 'Z' && !HasUpperLatin(chars, i + 1, end) && ShouldLowercaseSentenceStart(chars, i, end))
                    chars[i] = (char)(c + 32);
            }
            pendingSentenceEnd = c is '.' or '!' or '?';
        }
    }

    private bool ShouldLowercaseSentenceStart(char[] chars, int index, int end)
    {
        char upper = chars[index];
        if (!_map.LatinToArabic.ContainsKey(((char)(upper + 32)).ToString())) return false;
        _map.LatinToArabic.TryGetValue(upper.ToString(), out string? upperOutput);
        if (upperOutput is AlefWithHamzaAbove or AlefWithHamzaBelow or AlefWithMadda)
        {
            // Many Arabic words start with أ / إ / آ, so keep the capital, except before ل:
            // "ال" (the definite article) is far more common than "أل".
            return upperOutput == AlefWithHamzaAbove
                && index + 1 < end
                && _map.LatinToArabic.TryGetValue(chars[index + 1].ToString(), out string? next)
                && next == Lam;
        }
        return true;
    }

    private static bool HasUpperLatin(char[] chars, int from, int to)
    {
        for (int i = from; i < to; i++)
        {
            if (chars[i] is >= 'A' and <= 'Z') return true;
        }
        return false;
    }

    // ---------------------------------------------------------------- Arabic -> Latin

    private string ResolveEnglish(List<Segment> segments)
    {
        var output = new StringBuilder(segments.Count + 8);
        var ambiguous = new List<int>();
        int i = 0;
        while (i < segments.Count)
        {
            if (!TextScripts.IsAsciiLetters(segments[i].Primary))
            {
                output.Append(segments[i].Primary);
                i++;
                continue;
            }

            int end = i;
            ambiguous.Clear();
            while (end < segments.Count && TextScripts.IsAsciiLetters(segments[end].Primary))
            {
                if (segments[end].Alternative is not null) ambiguous.Add(end);
                end++;
            }

            if (ambiguous.Count == 0 || ambiguous.Count > MaxAmbiguousPerWord)
            {
                for (int k = i; k < end; k++) output.Append(segments[k].Primary);
            }
            else
            {
                output.Append(PickMostLikelyWord(segments, i, end, ambiguous));
            }
            i = end;
        }
        return output.ToString();
    }

    /// <summary>"لا" can be the B key or G followed by H: pick the spelling that forms a known word.</summary>
    private string PickMostLikelyWord(List<Segment> segments, int start, int end, List<int> ambiguous)
    {
        string best = string.Empty;
        int bestScore = int.MinValue;
        var candidate = new StringBuilder();
        int combinations = 1 << ambiguous.Count;
        for (int mask = 0; mask < combinations; mask++)
        {
            candidate.Clear();
            int a = 0;
            for (int k = start; k < end; k++)
            {
                if (a < ambiguous.Count && ambiguous[a] == k)
                {
                    candidate.Append(((mask >> a) & 1) == 1 ? segments[k].Alternative : segments[k].Primary);
                    a++;
                }
                else
                {
                    candidate.Append(segments[k].Primary);
                }
            }
            string word = candidate.ToString();
            int score = (_lexicon.Contains(word) ? 100 : 0) - BitOperations.PopCount((uint)mask);
            if (score > bestScore)
            {
                bestScore = score;
                best = word;
            }
        }
        return best;
    }

    // ---------------------------------------------------------------- shared helpers

    private readonly record struct Segment(string Primary, string? Alternative);

    private static List<Segment> Tokenize(string text, IReadOnlyDictionary<string, string> map, int maxKeyLength, bool allowAlternatives)
    {
        var segments = new List<Segment>(text.Length);
        int i = 0;
        while (i < text.Length)
        {
            bool matched = false;
            for (int length = Math.Min(maxKeyLength, text.Length - i); length >= 1; length--)
            {
                string key = text.Substring(i, length);
                if (!map.TryGetValue(key, out string? value)) continue;
                string? alternative = allowAlternatives && length > 1 ? SplitAlternative(key, value, map) : null;
                segments.Add(new Segment(value, alternative));
                i += length;
                matched = true;
                break;
            }
            if (matched) continue;

            int width = char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]) ? 2 : 1;
            segments.Add(new Segment(text.Substring(i, width), null));
            i += width;
        }
        return segments;
    }

    private static string? SplitAlternative(string key, string primary, IReadOnlyDictionary<string, string> map)
    {
        var builder = new StringBuilder();
        foreach (char c in key)
        {
            if (!map.TryGetValue(c.ToString(), out string? part)) return null;
            builder.Append(part);
        }
        string alternative = builder.ToString();
        return alternative != primary && TextScripts.IsLowerAsciiWord(alternative) && TextScripts.IsLowerAsciiWord(primary)
            ? alternative
            : null;
    }

    private enum WordKind { Neutral, Latin, Arabic }

    private readonly record struct Word(int Start, int End, WordKind Kind);

    private List<Word> SplitWords(string text)
    {
        var words = new List<Word>();
        int i = 0;
        while (i < text.Length)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
            if (i >= text.Length) break;
            int start = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i])) i++;
            words.Add(new Word(start, i, Classify(text, start, i)));
        }
        return words;
    }

    private WordKind Classify(string text, int start, int end)
    {
        int latin = 0, arabic = 0;
        for (int i = start; i < end; i++)
        {
            char c = text[i];
            if (TextScripts.IsArabicLetter(c)) arabic++;
            else if (TextScripts.IsBasicLatinLetter(c)) latin++;
            else if (_map.LatinToArabic.TryGetValue(c.ToString(), out string? arabicText) && TextScripts.ContainsArabicLetter(arabicText)) latin++;
        }
        if (latin > arabic) return WordKind.Latin;
        if (arabic > latin) return WordKind.Arabic;
        return WordKind.Neutral;
    }

    private static bool IsSentenceStart(string prefix)
    {
        for (int i = prefix.Length - 1; i >= 0; i--)
        {
            char c = prefix[i];
            if (c is '\r' or '\n') return true;
            if (char.IsWhiteSpace(c)) continue;
            return c is '.' or '!' or '?' or '\u061F';
        }
        return true;
    }
}
