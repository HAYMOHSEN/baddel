using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace Baddel.Services;

/// <summary>A global keyboard shortcut such as Ctrl + Alt + Space.</summary>
internal sealed record HotkeyGesture(ModifierKeys Modifiers, Key Key)
{
    public static HotkeyGesture Default { get; } = new(ModifierKeys.Control | ModifierKeys.Alt, Key.Space);

    /// <summary>Needs Ctrl, Alt or Win, unless it is a key nobody types with (F1–F24, Pause, Scroll Lock).</summary>
    public bool IsValid
    {
        get
        {
            if (Key == Key.None || IsModifierKey(Key)) return false;
            bool strongModifier = (Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) != 0;
            return strongModifier || IsStandaloneKey(Key);
        }
    }

    public uint NativeModifiers
    {
        get
        {
            uint value = 0;
            if ((Modifiers & ModifierKeys.Alt) != 0) value |= Native.MOD_ALT;
            if ((Modifiers & ModifierKeys.Control) != 0) value |= Native.MOD_CONTROL;
            if ((Modifiers & ModifierKeys.Shift) != 0) value |= Native.MOD_SHIFT;
            if ((Modifiers & ModifierKeys.Windows) != 0) value |= Native.MOD_WIN;
            return value;
        }
    }

    public IReadOnlyList<string> KeyLabels
    {
        get
        {
            var labels = new List<string>(5);
            if ((Modifiers & ModifierKeys.Windows) != 0) labels.Add("Win");
            if ((Modifiers & ModifierKeys.Control) != 0) labels.Add("Ctrl");
            if ((Modifiers & ModifierKeys.Alt) != 0) labels.Add("Alt");
            if ((Modifiers & ModifierKeys.Shift) != 0) labels.Add("Shift");
            if (Key != Key.None) labels.Add(KeyLabel(Key));
            return labels;
        }
    }

    public override string ToString() => string.Join(" + ", KeyLabels);

    public static HotkeyGesture? Parse(string? modifiers, string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || !Enum.TryParse(key, out Key parsedKey)) return null;
        ModifierKeys parsedModifiers = ModifierKeys.None;
        if (!string.IsNullOrWhiteSpace(modifiers) && !Enum.TryParse(modifiers, out parsedModifiers)) return null;
        var gesture = new HotkeyGesture(parsedModifiers, parsedKey);
        return gesture.IsValid ? gesture : null;
    }

    public static bool IsModifierKey(Key key) => key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
        or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.System;

    private static bool IsStandaloneKey(Key key) => key is >= Key.F1 and <= Key.F24 or Key.Pause or Key.Scroll;

    private static string KeyLabel(Key key) => key switch
    {
        Key.Space => "Space",
        Key.Pause => "Pause",
        Key.Scroll => "Scroll Lock",
        Key.Return => "Enter",
        Key.Back => "Backspace",
        Key.Escape => "Esc",
        Key.PageUp => "Page Up",
        Key.Next => "Page Down",
        Key.OemTilde => "`",
        Key.OemMinus => "-",
        Key.OemPlus => "=",
        Key.OemOpenBrackets => "[",
        Key.OemCloseBrackets => "]",
        Key.OemPipe => "\\",
        Key.OemSemicolon => ";",
        Key.OemQuotes => "'",
        Key.OemComma => ",",
        Key.OemPeriod => ".",
        Key.OemQuestion => "/",
        Key.OemBackslash => "\\",
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => "Num " + (char)('0' + (key - Key.NumPad0)),
        _ => key.ToString(),
    };
}
