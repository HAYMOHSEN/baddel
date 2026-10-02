using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Baddel.Services;

/// <summary>Sends the few key presses Baddel needs (copy, paste, select line) to the app in front.</summary>
internal static class InputSender
{
    private const ushort VK_SHIFT = 0x10;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_MENU = 0x12;
    private const ushort VK_END = 0x23;
    private const ushort VK_HOME = 0x24;
    private const ushort VK_C = 0x43;
    private const ushort VK_V = 0x56;
    private const ushort VK_LWIN = 0x5B;
    private const ushort VK_RWIN = 0x5C;
    private const ushort VK_UNASSIGNED = 0xFF;
    private static readonly IntPtr Signature = new(0x42444C);

    public static void Copy() => Chord(VK_CONTROL, VK_C, extended: false);

    public static void Paste() => Chord(VK_CONTROL, VK_V, extended: false);

    public static void SelectToLineStart() => Chord(VK_SHIFT, VK_HOME, extended: true);

    public static void MoveToLineEnd() => Send(Key(VK_END, up: false, extended: true), Key(VK_END, up: true, extended: true));

    /// <summary>A harmless key press that stops a released Alt or Win key from opening a menu.</summary>
    public static void SendNeutralKey() => Send(Key(VK_UNASSIGNED, up: false, extended: false), Key(VK_UNASSIGNED, up: true, extended: false));

    public static async Task<bool> WaitForModifiersReleasedAsync(TimeSpan timeout)
    {
        var watch = Stopwatch.StartNew();
        while (IsDown(VK_SHIFT) || IsDown(VK_CONTROL) || IsDown(VK_MENU) || IsDown(VK_LWIN) || IsDown(VK_RWIN))
        {
            if (watch.Elapsed > timeout) return false;
            await Task.Delay(10);
        }
        return true;
    }

    private static bool IsDown(int virtualKey) => (Native.GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private static void Chord(ushort modifier, ushort key, bool extended) => Send(
        Key(modifier, up: false, extended: false),
        Key(key, up: false, extended: extended),
        Key(key, up: true, extended: extended),
        Key(modifier, up: true, extended: false));

    private static Native.INPUT Key(ushort virtualKey, bool up, bool extended)
    {
        uint flags = 0;
        if (up) flags |= Native.KEYEVENTF_KEYUP;
        if (extended) flags |= Native.KEYEVENTF_EXTENDEDKEY;
        return new Native.INPUT
        {
            type = Native.INPUT_KEYBOARD,
            U = new Native.InputUnion
            {
                ki = new Native.KEYBDINPUT
                {
                    wVk = virtualKey,
                    wScan = (ushort)Native.MapVirtualKey(virtualKey, Native.MAPVK_VK_TO_VSC),
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = Signature,
                },
            },
        };
    }

    private static void Send(params Native.INPUT[] inputs)
    {
        uint sent = Native.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Native.INPUT>());
        if (sent != inputs.Length)
            Logger.Info($"SendInput delivered {sent} of {inputs.Length} events (error {Marshal.GetLastWin32Error()}).");
    }
}
