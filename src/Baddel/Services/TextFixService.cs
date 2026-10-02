using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using Baddel.Core;

namespace Baddel.Services;

internal enum FixKind
{
    Converted,
    Reverted,
    NothingSelected,
    NothingToConvert,
    ElevatedTarget,
    Terminal,
    Busy,
    Ignored,
    Failed,
}

internal readonly record struct FixOutcome(FixKind Kind, ConversionDirection Direction = ConversionDirection.None, IntPtr Foreground = default);

/// <summary>
/// What happens when the shortcut is pressed: copy the selection (or the current line), convert it,
/// paste it back, switch the keyboard language and restore the user's clipboard.
/// </summary>
internal sealed class TextFixService
{
    private static readonly TimeSpan UndoWindow = TimeSpan.FromMinutes(5);

    // Apps that capitalise the first letter of a sentence while you type.
    private static readonly HashSet<string> OfficeApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "WINWORD", "OUTLOOK", "POWERPNT", "EXCEL", "ONENOTE", "MSACCESS", "MSPUB", "VISIO",
    };

    // In a terminal, Ctrl+C stops the running command, so Baddel stays out of terminals.
    private static readonly HashSet<string> TerminalApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "WindowsTerminal", "OpenConsole", "conhost", "cmd", "powershell", "pwsh", "wsl", "mintty", "putty",
        "kitty", "alacritty", "wezterm-gui", "Hyper", "Tabby", "MobaXterm", "Termius",
    };

    private static readonly HashSet<string> TerminalWindowClasses = new(StringComparer.Ordinal)
    {
        "ConsoleWindowClass", "CASCADIA_HOSTING_WINDOW_CLASS", "PseudoConsoleWindow", "mintty", "PuTTY",
    };

    private readonly KeyboardLayoutService _layouts;
    private readonly SettingsService _settings;
    private bool _busy;
    private LastFix? _last;

    public TextFixService(KeyboardLayoutService layouts, SettingsService settings)
    {
        _layouts = layouts;
        _settings = settings;
    }

    private sealed record LastFix(string Original, string Result, ConversionDirection Direction, DateTime At);

    public async Task<FixOutcome> FixSelectionAsync(HotkeyGesture? gesture)
    {
        IntPtr foreground = Native.GetForegroundWindow();
        if (_busy) return new FixOutcome(FixKind.Busy, Foreground: foreground);
        _busy = true;
        try
        {
            return await FixAsync(foreground, gesture);
        }
        catch (Exception ex)
        {
            Logger.Error("Fixing the selection failed.", ex);
            return new FixOutcome(FixKind.Failed, Foreground: foreground);
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task<FixOutcome> FixAsync(IntPtr foreground, HotkeyGesture? gesture)
    {
        if (foreground == IntPtr.Zero) return new FixOutcome(FixKind.Ignored);
        uint threadId = Native.GetWindowThreadProcessId(foreground, out uint processId);
        string processName = ProcessHelper.GetName(processId);
        if (TerminalApps.Contains(processName) || TerminalWindowClasses.Contains(Native.GetWindowClassName(foreground)))
            return new FixOutcome(FixKind.Terminal, Foreground: foreground);

        if (gesture is not null && (gesture.Modifiers & (ModifierKeys.Alt | ModifierKeys.Windows)) != 0)
            InputSender.SendNeutralKey();
        if (!await InputSender.WaitForModifiersReleasedAsync(TimeSpan.FromSeconds(2)))
            return new FixOutcome(FixKind.Ignored, Foreground: foreground);

        AppSettings settings = _settings.Current;
        ClipboardSnapshot? snapshot = settings.RestoreClipboard ? ClipboardSnapshot.Capture() : null;
        try
        {
            CopyResult copy = await ClipboardService.CopySelectionAsync(processName);
            bool selectedForUser = false;
            if (copy.IsEmpty && settings.SelectLineWhenNothingSelected)
            {
                InputSender.SelectToLineStart();
                await Task.Delay(50);
                copy = await ClipboardService.CopySelectionAsync(processName);
                selectedForUser = true;
            }
            if (copy.IsEmpty || copy.Text is null)
            {
                FixKind kind = ProcessHelper.IsElevated(processId) ? FixKind.ElevatedTarget : FixKind.NothingSelected;
                return new FixOutcome(kind, Foreground: foreground);
            }

            string selected = copy.Text;
            LayoutPair pair = _layouts.GetActivePair();
            string output;
            ConversionDirection direction;
            LastFix? last = _last;
            bool reverting = last is not null
                && string.Equals(selected, last.Result, StringComparison.Ordinal)
                && DateTime.UtcNow - last.At < UndoWindow;
            if (reverting && last is not null)
            {
                // Pressing the shortcut again on the text we just produced puts the original back.
                output = last.Original;
                direction = last.Direction == ConversionDirection.LatinToArabic
                    ? ConversionDirection.ArabicToLatin
                    : ConversionDirection.LatinToArabic;
            }
            else
            {
                var options = new ConversionOptions
                {
                    DirectionWhenTied = KeyboardLayoutService.IsArabicLayout(KeyboardLayoutService.GetLayoutOfThread(threadId))
                        ? ConversionDirection.ArabicToLatin
                        : ConversionDirection.LatinToArabic,
                    FixAutoCapitalization = OfficeApps.Contains(processName),
                };
                ConversionResult result = selectedForUser
                    ? pair.Converter.ConvertLatestRun(selected, options)
                    : pair.Converter.Convert(selected, options);
                if (!result.Changed)
                {
                    if (selectedForUser) InputSender.MoveToLineEnd();
                    return new FixOutcome(FixKind.NothingToConvert, Foreground: foreground);
                }
                output = result.Text;
                direction = result.Direction;
            }

            await ClipboardService.SetTextAsync(output, keepOutOfHistory: true);
            InputSender.Paste();
            // Give the app time to read the clipboard; apps that were slow to copy get more time.
            await Task.Delay((int)Math.Clamp(copy.ElapsedMs * 3 + 150, 250, 1500));
            if (settings.SwitchKeyboardLayout) pair.SwitchKeyboard(foreground, direction);
            _last = reverting ? null : new LastFix(selected, output, direction, DateTime.UtcNow);
            return new FixOutcome(reverting ? FixKind.Reverted : FixKind.Converted, direction, foreground);
        }
        finally
        {
            if (snapshot is not null) await snapshot.RestoreAsync();
        }
    }
}
