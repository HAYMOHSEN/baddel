using System;
using System.Globalization;
using System.Linq;
using System.Windows;

namespace Baddel.Services;

/// <summary>Interface language: swaps Resources/Strings.ar.xaml and Strings.en.xaml at run time.</summary>
internal static class Loc
{
    public static string Language { get; private set; } = "ar";

    public static bool IsRtl => Language == "ar";

    public static FlowDirection Flow => IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    public static event EventHandler? LanguageChanged;

    public static void Apply(string? language)
    {
        Language = language == "en" ? "en" : "ar";
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var fresh = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Resources/Strings.{Language}.xaml", UriKind.Absolute),
        };
        ResourceDictionary? existing = dictionaries.FirstOrDefault(
            d => d.Source?.OriginalString.Contains("Strings.", StringComparison.OrdinalIgnoreCase) == true);
        if (existing is null) dictionaries.Add(fresh);
        else dictionaries[dictionaries.IndexOf(existing)] = fresh;
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string T(string key) => Application.Current.TryFindResource(key) as string ?? key;

    public static string F(string key, params object[] args) => string.Format(CultureInfo.CurrentCulture, T(key), args);
}
