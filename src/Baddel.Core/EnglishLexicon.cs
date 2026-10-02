using System;
using System.Collections.Generic;

namespace Baddel.Core;

/// <summary>
/// English words that contain "gh". On the Arabic keyboard the keys G and H produce "لا",
/// exactly like the B key, so this list decides whether "لا" should become "gh" or "b".
/// </summary>
public sealed partial class EnglishLexicon
{
    private readonly HashSet<string> _words;

    public EnglishLexicon(IEnumerable<string> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        _words = new HashSet<string>(StringComparer.Ordinal);
        foreach (string word in words)
        {
            if (!string.IsNullOrWhiteSpace(word)) _words.Add(word.Trim().ToLowerInvariant());
        }
    }

    public static EnglishLexicon Shared { get; } =
        new(GhWordsData.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public int Count => _words.Count;

    public bool Contains(string word) => _words.Contains(word.ToLowerInvariant());
}
