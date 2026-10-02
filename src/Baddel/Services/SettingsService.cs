using System;
using System.IO;
using System.Text.Json;

namespace Baddel.Services;

internal sealed class AppSettings
{
    public int Version { get; set; } = 1;
    public string HotkeyModifiers { get; set; } = "Control, Alt";
    public string HotkeyKey { get; set; } = "Space";
    public bool SelectLineWhenNothingSelected { get; set; } = true;
    public bool SwitchKeyboardLayout { get; set; } = true;
    public bool RestoreClipboard { get; set; } = true;
    public bool ShowNotifications { get; set; } = true;
    public string Language { get; set; } = "ar";
    public string Theme { get; set; } = "System";
    public string? ArabicLayout { get; set; }
    public string? LatinLayout { get; set; }
    public bool FirstRunCompleted { get; set; }
    public bool TrayHintShown { get; set; }
    public int FixCount { get; set; }
    public bool RatingPromptShown { get; set; }
}

/// <summary>Reads and writes settings.json in the user's local app data folder.</summary>
internal sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path = Path.Combine(Logger.Folder, "settings.json");

    public AppSettings Current { get; private set; } = new();

    public void Load()
    {
        try
        {
            if (File.Exists(_path))
                Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            Logger.Error("Settings could not be read; defaults restored.", ex);
            Current = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Logger.Folder);
            string temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(Current, JsonOptions));
            File.Move(temporary, _path, overwrite: true);
        }
        catch (Exception ex)
        {
            Logger.Error("Settings could not be saved.", ex);
        }
    }

    public void Update(Action<AppSettings> change)
    {
        change(Current);
        Save();
    }
}
