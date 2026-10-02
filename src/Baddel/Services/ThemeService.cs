using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Baddel.Services;

/// <summary>Light/dark handling. Controls follow WPF's Fluent theme; these brushes cover Baddel's own surfaces.</summary>
internal static class ThemeService
{
    public static bool IsDark(string? setting) => setting switch
    {
        "Dark" => true,
        "Light" => false,
        _ => SystemUsesDarkTheme(),
    };

    public static bool SystemUsesDarkTheme()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static ThemeMode ToThemeMode(string? setting) => setting switch
    {
        "Light" => ThemeMode.Light,
        "Dark" => ThemeMode.Dark,
        _ => ThemeMode.System,
    };

    public static void ApplyBrushes(bool dark)
    {
        ResourceDictionary resources = Application.Current.Resources;
        resources["Baddel.CardBackground"] = Brush(dark ? "#0DFFFFFF" : "#B3FFFFFF");
        resources["Baddel.CardBorder"] = Brush(dark ? "#33000000" : "#14000000");
        resources["Baddel.TextSecondary"] = Brush(dark ? "#C5FFFFFF" : "#9E000000");
        resources["Baddel.KeycapFace"] = Brush(dark ? "#2D323D" : "#FFFFFF");
        resources["Baddel.KeycapEdge"] = Brush(dark ? "#14171E" : "#C3C9D4");
        resources["Baddel.Success"] = Brush(dark ? "#6CCB5F" : "#0F7B0F");
        resources["Baddel.Caution"] = Brush(dark ? "#FCE100" : "#9D5D00");
        resources["Baddel.WarningBackground"] = Brush(dark ? "#3D3418" : "#FFF4CE");
    }

    private static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
