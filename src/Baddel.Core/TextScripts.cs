using System.Text;

namespace Baddel.Core;

/// <summary>Helpers for recognising Arabic and basic Latin characters.</summary>
public static class TextScripts
{
    /// <summary>True for Arabic-script letters (not diacritics, digits or punctuation).</summary>
    public static bool IsArabicLetter(char c)
    {
        bool arabicBlock = c is >= '\u0600' and <= '\u06FF'
            or >= '\u0750' and <= '\u077F'
            or >= '\u08A0' and <= '\u08FF'
            or >= '\uFB50' and <= '\uFDFF'
            or >= '\uFE70' and <= '\uFEFE';
        return arabicBlock && char.IsLetter(c);
    }

    /// <summary>True for the 52 ASCII letters.</summary>
    public static bool IsBasicLatinLetter(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

    public static bool ContainsArabicLetter(string text)
    {
        foreach (char c in text)
        {
            if (IsArabicLetter(c)) return true;
        }
        return false;
    }

    internal static bool IsAsciiLetters(string text)
    {
        if (text.Length == 0) return false;
        foreach (char c in text)
        {
            if (!IsBasicLatinLetter(c)) return false;
        }
        return true;
    }

    internal static bool IsLowerAsciiWord(string text)
    {
        if (text.Length == 0) return false;
        foreach (char c in text)
        {
            if (c is < 'a' or > 'z') return false;
        }
        return true;
    }

    /// <summary>Turns Arabic presentation forms (such as the lam-alef ligature) into ordinary letters.</summary>
    public static string NormalizeArabicPresentationForms(string text)
    {
        StringBuilder? builder = null;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c is >= '\uFB50' and <= '\uFDFF' or >= '\uFE70' and <= '\uFEFE')
            {
                builder ??= new StringBuilder(text, 0, i, text.Length + 8);
                builder.Append(c.ToString().Normalize(NormalizationForm.FormKC));
            }
            else
            {
                builder?.Append(c);
            }
        }
        return builder?.ToString() ?? text;
    }
}
