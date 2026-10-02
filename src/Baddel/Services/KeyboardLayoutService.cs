using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Baddel.Core;
using Microsoft.Win32;

namespace Baddel.Services;

/// <summary>A keyboard layout installed in Windows.</summary>
internal sealed record InstalledLayout(IntPtr Handle, string DisplayName)
{
    public int LanguageId => (int)(Handle.ToInt64() & 0xFFFF);
    public bool IsArabic => (LanguageId & 0x3FF) == 0x01;
    public bool IsEnglish => (LanguageId & 0x3FF) == 0x09;
    public string Id => Handle.ToInt64().ToString("X", CultureInfo.InvariantCulture);
}

/// <summary>The Arabic and Latin layouts in use, and the converter built from them.</summary>
internal sealed class LayoutPair
{
    public LayoutPair(LayoutConverter converter, IntPtr arabicLayout, IntPtr latinLayout, bool usesInstalledLayouts)
    {
        Converter = converter;
        ArabicLayout = arabicLayout;
        LatinLayout = latinLayout;
        UsesInstalledLayouts = usesInstalledLayouts;
    }

    public LayoutConverter Converter { get; }
    public IntPtr ArabicLayout { get; }
    public IntPtr LatinLayout { get; }
    public bool UsesInstalledLayouts { get; }

    /// <summary>After a fix, switch the keyboard to the language the user meant to type in.</summary>
    public void SwitchKeyboard(IntPtr window, ConversionDirection direction)
    {
        IntPtr target = direction switch
        {
            ConversionDirection.LatinToArabic => ArabicLayout,
            ConversionDirection.ArabicToLatin => LatinLayout,
            _ => IntPtr.Zero,
        };
        if (target != IntPtr.Zero) KeyboardLayoutService.RequestLayout(window, target);
    }
}

/// <summary>Reads the keyboard layouts installed in Windows and builds the character map from them.</summary>
internal sealed class KeyboardLayoutService
{
    private static readonly uint[] ScanCodes = BuildScanCodes();
    private readonly SettingsService _settings;
    private LayoutPair? _cached;
    private (IntPtr Arabic, IntPtr Latin) _cachedKey;

    public KeyboardLayoutService(SettingsService settings) => _settings = settings;

    public static IReadOnlyList<InstalledLayout> GetInstalledLayouts()
    {
        int count = Native.GetKeyboardLayoutList(0, null);
        if (count <= 0) return Array.Empty<InstalledLayout>();
        var handles = new IntPtr[count];
        count = Native.GetKeyboardLayoutList(count, handles);
        var layouts = new List<InstalledLayout>(count);
        for (int i = 0; i < count; i++) layouts.Add(new InstalledLayout(handles[i], DescribeLayout(handles[i])));
        return layouts;
    }

    public LayoutPair GetActivePair()
    {
        IReadOnlyList<InstalledLayout> layouts = GetInstalledLayouts();
        IntPtr arabic = Choose(layouts, _settings.Current.ArabicLayout, l => l.IsArabic);
        IntPtr latin = Choose(layouts, _settings.Current.LatinLayout, l => l.IsEnglish);
        if (latin == IntPtr.Zero) latin = Choose(layouts, null, l => !l.IsArabic);

        if (_cached is not null && _cachedKey == (arabic, latin)) return _cached;

        LayoutMap? map = null;
        if (arabic != IntPtr.Zero && latin != IntPtr.Zero)
        {
            try { map = BuildMap(latin, arabic); }
            catch (Exception ex) { Logger.Error("The keyboard map could not be read from Windows.", ex); }
        }
        _cached = new LayoutPair(new LayoutConverter(map ?? LayoutMap.Standard), arabic, latin, map is not null);
        _cachedKey = (arabic, latin);
        return _cached;
    }

    public void Invalidate() => _cached = null;

    public static bool IsArabicLayout(IntPtr layout) => (layout.ToInt64() & 0x3FF) == 0x01;

    public static IntPtr GetLayoutOfThread(uint threadId) => Native.GetKeyboardLayout(threadId);

    /// <summary>Asks the window that has the keyboard focus to switch to the given layout.</summary>
    public static void RequestLayout(IntPtr window, IntPtr layout)
    {
        uint thread = Native.GetWindowThreadProcessId(window, out _);
        var info = new Native.GUITHREADINFO { cbSize = Marshal.SizeOf<Native.GUITHREADINFO>() };
        IntPtr target = Native.GetGUIThreadInfo(thread, ref info) && info.hwndFocus != IntPtr.Zero ? info.hwndFocus : window;
        Native.PostMessage(target, Native.WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, layout);
    }

    public static string DescribeLayout(IntPtr layout)
    {
        int languageId = (int)(layout.ToInt64() & 0xFFFF);
        string language;
        try { language = CultureInfo.GetCultureInfo(languageId).NativeName; }
        catch (CultureNotFoundException) { language = "0x" + languageId.ToString("X4", CultureInfo.InvariantCulture); }
        string? layoutName = ReadLayoutName(layout, languageId);
        return string.IsNullOrEmpty(layoutName) || language.Contains(layoutName, StringComparison.OrdinalIgnoreCase)
            ? language
            : $"{language} — {layoutName}";
    }

    private static IntPtr Choose(IReadOnlyList<InstalledLayout> layouts, string? preferredId, Func<InstalledLayout, bool> fallback)
    {
        if (!string.IsNullOrEmpty(preferredId))
        {
            InstalledLayout? preferred = layouts.FirstOrDefault(l => l.Id == preferredId);
            if (preferred is not null) return preferred.Handle;
        }
        return layouts.FirstOrDefault(fallback)?.Handle ?? IntPtr.Zero;
    }

    /// <summary>For every physical key and modifier state, records what each layout types.</summary>
    private static LayoutMap? BuildMap(IntPtr latin, IntPtr arabic)
    {
        var pairs = new List<(string Latin, string Arabic)>();
        // Plain, Shift, AltGr, Shift+AltGr – in that order, so plain keys win any conflict.
        foreach ((bool shift, bool altGr) in new[] { (false, false), (true, false), (false, true), (true, true) })
        {
            var keyState = new byte[256];
            if (shift) keyState[0x10] = 0x80;
            if (altGr)
            {
                keyState[0x11] = 0x80;
                keyState[0x12] = 0x80;
            }
            foreach (uint scanCode in ScanCodes)
            {
                string? latinText = Translate(scanCode, keyState, latin);
                string? arabicText = Translate(scanCode, keyState, arabic);
                if (latinText is null || arabicText is null || latinText == arabicText) continue;
                pairs.Add((latinText, arabicText));
            }
        }
        LayoutMap map = LayoutMap.FromPairs(DescribeLayout(latin) + " / " + DescribeLayout(arabic), pairs);
        return map.ArabicLetterCount >= 20 ? map : null;
    }

    private static string? Translate(uint scanCode, byte[] keyState, IntPtr layout)
    {
        uint virtualKey = Native.MapVirtualKeyEx(scanCode, Native.MAPVK_VSC_TO_VK, layout);
        if (virtualKey == 0) return null;
        var buffer = new char[8];
        int written = Native.ToUnicodeEx(virtualKey, scanCode, keyState, buffer, buffer.Length, Native.TOUNICODE_NO_STATE_CHANGE, layout);
        if (written == 0) return null;
        string text = written < 0 ? new string(buffer, 0, 1) : new string(buffer, 0, Math.Min(written, buffer.Length));
        foreach (char c in text)
        {
            if (c < 0x20 || c == 0x7F) return null;
        }
        return text;
    }

    private static uint[] BuildScanCodes()
    {
        var codes = new List<uint>();
        for (uint sc = 0x02; sc <= 0x0D; sc++) codes.Add(sc); // number row
        for (uint sc = 0x10; sc <= 0x1B; sc++) codes.Add(sc); // Q row
        for (uint sc = 0x1E; sc <= 0x29; sc++) codes.Add(sc); // A row and the ` key
        for (uint sc = 0x2B; sc <= 0x35; sc++) codes.Add(sc); // \ and the Z row
        codes.Add(0x56);                                      // extra key on ISO keyboards
        return codes.ToArray();
    }

    private static string? ReadLayoutName(IntPtr layout, int languageId)
    {
        const string root = @"SYSTEM\CurrentControlSet\Control\Keyboard Layouts";
        try
        {
            int device = (int)((layout.ToInt64() >> 16) & 0xFFFF);
            string? klid = null;
            if ((device & 0xF000) == 0xF000)
            {
                int layoutId = device & 0x0FFF;
                using RegistryKey? layouts = Registry.LocalMachine.OpenSubKey(root);
                if (layouts is null) return null;
                foreach (string name in layouts.GetSubKeyNames())
                {
                    using RegistryKey? key = layouts.OpenSubKey(name);
                    if (key?.GetValue("Layout Id") is string id
                        && int.TryParse(id, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int value)
                        && value == layoutId)
                    {
                        klid = name;
                        break;
                    }
                }
            }
            else
            {
                klid = (device == 0 ? languageId : device).ToString("X8", CultureInfo.InvariantCulture);
            }
            if (klid is null) return null;

            using RegistryKey? entry = Registry.LocalMachine.OpenSubKey(root + @"\" + klid);
            if (entry is null) return null;
            if (entry.GetValue("Layout Display Name") is string indirect && indirect.StartsWith('@'))
            {
                var buffer = new StringBuilder(260);
                if (Native.SHLoadIndirectString(Environment.ExpandEnvironmentVariables(indirect), buffer, buffer.Capacity, IntPtr.Zero) == 0)
                    return buffer.ToString();
            }
            return entry.GetValue("Layout Text") as string;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
