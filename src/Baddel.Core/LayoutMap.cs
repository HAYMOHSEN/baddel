using System;
using System.Collections.Generic;
using System.Linq;

namespace Baddel.Core;

/// <summary>
/// Links what each physical key produces on a Latin keyboard layout to what the same key
/// produces on an Arabic layout. On Windows the map is read from the layouts installed on the PC;
/// <see cref="Standard"/> (US QWERTY and Arabic 101) is the fallback.
/// </summary>
public sealed class LayoutMap
{
    private readonly Dictionary<string, string> _latinToArabic;
    private readonly Dictionary<string, string> _arabicToLatin;

    private LayoutMap(string name, Dictionary<string, string> latinToArabic, Dictionary<string, string> arabicToLatin)
    {
        Name = name;
        _latinToArabic = latinToArabic;
        _arabicToLatin = arabicToLatin;
        MaxLatinKeyLength = latinToArabic.Count == 0 ? 1 : latinToArabic.Keys.Max(k => k.Length);
        MaxArabicKeyLength = arabicToLatin.Count == 0 ? 1 : arabicToLatin.Keys.Max(k => k.Length);
    }

    public string Name { get; }
    public IReadOnlyDictionary<string, string> LatinToArabic => _latinToArabic;
    public IReadOnlyDictionary<string, string> ArabicToLatin => _arabicToLatin;
    public int MaxLatinKeyLength { get; }
    public int MaxArabicKeyLength { get; }

    /// <summary>How many Arabic letters the map can translate; used to reject unusable layout pairs.</summary>
    public int ArabicLetterCount => _arabicToLatin.Keys.Count(k => k.Length == 1 && TextScripts.IsArabicLetter(k[0]));

    /// <summary>US QWERTY linked to the standard Windows "Arabic (101)" layout.</summary>
    public static LayoutMap Standard { get; } = FromPairs("US QWERTY / Arabic (101)", StandardPairs());

    /// <summary>Builds a map from (latin, arabic) pairs in priority order: the first pair wins a conflict.</summary>
    public static LayoutMap FromPairs(string name, IEnumerable<(string Latin, string Arabic)> pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        var latinToArabic = new Dictionary<string, string>(StringComparer.Ordinal);
        var arabicToLatin = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string latin, string arabic) in pairs)
        {
            if (string.IsNullOrEmpty(latin) || string.IsNullOrEmpty(arabic)) continue;
            latinToArabic.TryAdd(latin, arabic);
            arabicToLatin.TryAdd(arabic, latin);
        }
        AddTypingExtras(latinToArabic, arabicToLatin);
        return new LayoutMap(name, latinToArabic, arabicToLatin);
    }

    private static void AddTypingExtras(Dictionary<string, string> latinToArabic, Dictionary<string, string> arabicToLatin)
    {
        // Word and Outlook turn ' and " into curly quotes as you type; treat them as the keys that were pressed.
        if (latinToArabic.TryGetValue("'", out string? apostrophe))
        {
            latinToArabic.TryAdd("\u2018", apostrophe);
            latinToArabic.TryAdd("\u2019", apostrophe);
        }
        if (latinToArabic.TryGetValue("\"", out string? quote))
        {
            latinToArabic.TryAdd("\u201C", quote);
            latinToArabic.TryAdd("\u201D", quote);
        }
        // Arabic-Indic and Persian digits become Western digits.
        for (int digit = 0; digit <= 9; digit++)
        {
            string western = ((char)('0' + digit)).ToString();
            arabicToLatin.TryAdd(((char)(0x0660 + digit)).ToString(), western);
            arabicToLatin.TryAdd(((char)(0x06F0 + digit)).ToString(), western);
        }
    }

    private static IEnumerable<(string Latin, string Arabic)> StandardPairs() => new (string, string)[]
    {
        // Unshifted keys
        ("`", "\u0630"),        // ذ
        ("q", "\u0636"),        // ض
        ("w", "\u0635"),        // ص
        ("e", "\u062B"),        // ث
        ("r", "\u0642"),        // ق
        ("t", "\u0641"),        // ف
        ("y", "\u063A"),        // غ
        ("u", "\u0639"),        // ع
        ("i", "\u0647"),        // ه
        ("o", "\u062E"),        // خ
        ("p", "\u062D"),        // ح
        ("[", "\u062C"),        // ج
        ("]", "\u062F"),        // د
        ("a", "\u0634"),        // ش
        ("s", "\u0633"),        // س
        ("d", "\u064A"),        // ي
        ("f", "\u0628"),        // ب
        ("g", "\u0644"),        // ل
        ("h", "\u0627"),        // ا
        ("j", "\u062A"),        // ت
        ("k", "\u0646"),        // ن
        ("l", "\u0645"),        // م
        (";", "\u0643"),        // ك
        ("'", "\u0637"),        // ط
        ("z", "\u0626"),        // ئ
        ("x", "\u0621"),        // ء
        ("c", "\u0624"),        // ؤ
        ("v", "\u0631"),        // ر
        ("b", "\u0644\u0627"),  // لا
        ("n", "\u0649"),        // ى
        ("m", "\u0629"),        // ة
        (",", "\u0648"),        // و
        (".", "\u0632"),        // ز
        ("/", "\u0638"),        // ظ
        // Shifted keys
        ("~", "\u0651"),        // shadda
        ("(", ")"),             // the Arabic layout swaps the parentheses
        (")", "("),
        ("Q", "\u064E"),        // fatha
        ("W", "\u064B"),        // fathatan
        ("E", "\u064F"),        // damma
        ("R", "\u064C"),        // dammatan
        ("T", "\u0644\u0625"),  // لإ
        ("Y", "\u0625"),        // إ
        ("U", "\u2018"),        // ‘
        ("I", "\u00F7"),        // ÷
        ("O", "\u00D7"),        // ×
        ("P", "\u061B"),        // ؛
        ("{", "<"),
        ("}", ">"),
        ("A", "\u0650"),        // kasra
        ("S", "\u064D"),        // kasratan
        ("D", "]"),
        ("F", "["),
        ("G", "\u0644\u0623"),  // لأ
        ("H", "\u0623"),        // أ
        ("J", "\u0640"),        // tatweel
        ("K", "\u060C"),        // ،
        ("L", "/"),
        ("Z", "~"),
        ("X", "\u0652"),        // sukun
        ("C", "}"),
        ("V", "{"),
        ("B", "\u0644\u0622"),  // لآ
        ("N", "\u0622"),        // آ
        ("M", "\u2019"),        // ’
        ("<", ","),
        (">", "."),
        ("?", "\u061F"),        // ؟
    };
}
