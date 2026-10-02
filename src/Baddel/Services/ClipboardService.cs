using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;

namespace Baddel.Services;

internal readonly record struct CopyResult(string? Text, long ElapsedMs)
{
    public bool IsEmpty => string.IsNullOrEmpty(Text);
}

internal static class ClipboardService
{
    private const int CopyTimeoutMs = 700;

    // Visual Studio and Scintilla editors copy the whole line when nothing is selected and mark it like this.
    private static readonly string[] LineCopyFormats = { "MSDEVLineSelect", "VisualStudioEditorOperationsLineCutCopyClipboardTag" };

    // Editors that copy the whole current line when nothing is selected.
    private static readonly HashSet<string> LineCopyEditors = new(StringComparer.OrdinalIgnoreCase)
    {
        "Code", "Code - Insiders", "Cursor", "Windsurf", "VSCodium", "sublime_text", "devenv", "notepad++",
        "idea64", "pycharm64", "webstorm64", "rider64", "clion64", "goland64", "phpstorm64", "studio64",
    };

    /// <summary>Sends Ctrl+C and waits for the app to put the selection on the clipboard.</summary>
    public static async Task<CopyResult> CopySelectionAsync(string processName)
    {
        uint before = Native.GetClipboardSequenceNumber();
        var watch = Stopwatch.StartNew();
        InputSender.Copy();
        while (watch.ElapsedMilliseconds < CopyTimeoutMs)
        {
            await Task.Delay(15);
            if (Native.GetClipboardSequenceNumber() == before) continue;
            await Task.Delay(30); // let the app finish writing every format
            long elapsed = watch.ElapsedMilliseconds;
            string? text = await TryGetTextAsync();
            if (string.IsNullOrEmpty(text) || IsWholeLineCopy(processName, text)) return new CopyResult(null, elapsed);
            return new CopyResult(text, elapsed);
        }
        return new CopyResult(null, watch.ElapsedMilliseconds);
    }

    public static async Task<string?> TryGetTextAsync()
    {
        for (int attempt = 0; attempt < 6; attempt++)
        {
            try
            {
                return Clipboard.ContainsText(TextDataFormat.UnicodeText) ? Clipboard.GetText(TextDataFormat.UnicodeText) : null;
            }
            catch (ExternalException)
            {
                await Task.Delay(25);
            }
        }
        return null;
    }

    public static async Task SetTextAsync(string text, bool keepOutOfHistory)
    {
        var data = new DataObject();
        data.SetData(DataFormats.UnicodeText, text);
        if (keepOutOfHistory) KeepOutOfHistory(data);
        await RetryAsync(() => Clipboard.SetDataObject(data, true));
    }

    /// <summary>Windows 10 1809+: keep temporary text out of Clipboard history (Win+V) and cloud sync.</summary>
    internal static void KeepOutOfHistory(DataObject data)
    {
        data.SetData("CanIncludeInClipboardHistory", new MemoryStream(BitConverter.GetBytes(0)), false);
        data.SetData("CanUploadToCloudClipboard", new MemoryStream(BitConverter.GetBytes(0)), false);
    }

    internal static async Task RetryAsync(Action action)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (ExternalException) when (attempt < 8)
            {
                await Task.Delay(30);
            }
        }
    }

    private static bool IsWholeLineCopy(string processName, string text)
    {
        foreach (string format in LineCopyFormats)
        {
            try
            {
                if (Clipboard.ContainsData(format)) return true;
            }
            catch (ExternalException)
            {
                // ignore and keep checking
            }
        }
        if (!LineCopyEditors.Contains(processName)) return false;
        int newline = text.IndexOf('\n');
        return newline == text.Length - 1;
    }
}

/// <summary>A copy of everything on the clipboard, so it can be put back after a fix.</summary>
internal sealed class ClipboardSnapshot
{
    private readonly DataObject? _data;

    private ClipboardSnapshot(DataObject? data) => _data = data;

    /// <summary>Returns null when the clipboard can't be read (nothing will be restored then).</summary>
    public static ClipboardSnapshot? Capture()
    {
        try
        {
            IDataObject? current = Clipboard.GetDataObject();
            string[] formats = current?.GetFormats(false) ?? Array.Empty<string>();
            if (current is null || formats.Length == 0) return new ClipboardSnapshot(null);

            var copy = new DataObject();
            foreach (string format in formats)
            {
                try
                {
                    object? value = current.GetData(format, false);
                    if (value is Stream stream)
                    {
                        var buffer = new MemoryStream();
                        if (stream.CanSeek) stream.Position = 0;
                        stream.CopyTo(buffer);
                        buffer.Position = 0;
                        value = buffer;
                    }
                    if (value is not null) copy.SetData(format, value, false);
                }
                catch (Exception)
                {
                    // Some formats can't be read back; skip them.
                }
            }
            if (!copy.GetDataPresent("CanIncludeInClipboardHistory", false))
                copy.SetData("CanIncludeInClipboardHistory", new MemoryStream(BitConverter.GetBytes(0)), false);
            return new ClipboardSnapshot(copy);
        }
        catch (Exception ex)
        {
            Logger.Error("The clipboard could not be saved.", ex);
            return null;
        }
    }

    public async Task RestoreAsync()
    {
        try
        {
            if (_data is null) await ClipboardService.RetryAsync(Clipboard.Clear);
            else await ClipboardService.RetryAsync(() => Clipboard.SetDataObject(_data, true));
        }
        catch (Exception ex)
        {
            Logger.Error("The clipboard could not be restored.", ex);
        }
    }
}
