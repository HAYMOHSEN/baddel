using System;
using System.Threading.Tasks;
using System.Windows;
using Baddel.Core;
using Baddel.Services;
using Baddel.Views;
using Microsoft.Win32;
using WinForms = System.Windows.Forms;

namespace Baddel;

/// <summary>Runs in the notification area and wires the shortcut, the tray icon and the windows together.</summary>
public partial class App : Application
{
    private const int RatingPromptAfterFixes = 25;

    private SingleInstance? _singleInstance;
    private HotkeyService? _hotkeys;
    private TrayIconService? _tray;
    private TextFixService? _fixer;
    private OsdWindow? _osd;
    private MainWindow? _mainWindow;
    private bool _paused;
    private bool _suspendedForCapture;
    private bool _ratingBalloonPending;

    internal SettingsService Settings { get; } = new();
    internal KeyboardLayoutService Layouts { get; private set; } = null!;
    internal HotkeyGesture CurrentGesture { get; private set; } = HotkeyGesture.Default;
    internal bool IsExiting { get; private set; }
    internal bool IsPaused => _paused;
    internal bool HotkeyRegistrationFailed { get; private set; }

    internal event EventHandler? StatusChanged;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        RegisterErrorHandlers();

        _singleInstance = new SingleInstance("Baddel.HaniMuhsen");
        if (!_singleInstance.IsFirstInstance)
        {
            _singleInstance.SignalFirstInstance();
            Shutdown();
            return;
        }
        _singleInstance.ActivationRequested += (_, _) => Dispatcher.BeginInvoke(new Action(() => ShowMainWindow()));

        Settings.Load();
        Loc.Apply(Settings.Current.Language);
        ThemeService.ApplyBrushes(ThemeService.IsDark(Settings.Current.Theme));
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        WinForms.Application.EnableVisualStyles();

        Layouts = new KeyboardLayoutService(Settings);
        _fixer = new TextFixService(Layouts, Settings);
        _osd = new OsdWindow();

        _hotkeys = new HotkeyService();
        _hotkeys.Pressed += OnHotkeyPressed;
        CurrentGesture = HotkeyGesture.Parse(Settings.Current.HotkeyModifiers, Settings.Current.HotkeyKey) ?? HotkeyGesture.Default;
        RegisterCurrentHotkey();

        _tray = new TrayIconService();
        _tray.OpenRequested += (_, _) => ShowMainWindow();
        _tray.SettingsRequested += (_, _) => ShowMainWindow(MainWindowTab.Settings);
        _tray.FixClipboardRequested += async (_, _) => await FixClipboardAsync();
        _tray.PauseToggled += (_, _) => SetPaused(!_paused);
        _tray.ExitRequested += (_, _) => ExitApp();
        _tray.BalloonClicked += OnTrayBalloonClicked;
        RefreshTray();
        _tray.Show();

        bool launchedAtStartup = StartupService.WasLaunchedAtStartup(e.Args);
        if (!launchedAtStartup || !Settings.Current.FirstRunCompleted || HotkeyRegistrationFailed)
            ShowMainWindow();
        Logger.Info($"Started. Packaged: {PackageInfo.IsPackaged}. At startup: {launchedAtStartup}.");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _hotkeys?.Dispose();
        _tray?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        Settings.Save();
        base.OnSessionEnding(e);
    }

    internal void ShowMainWindow(MainWindowTab? tab = null)
    {
        if (IsExiting) return;
        _mainWindow ??= new MainWindow(this);
        if (tab is { } selected) _mainWindow.SelectTab(selected);
        if (!_mainWindow.IsVisible) _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized) _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
        _mainWindow.Topmost = true;
        _mainWindow.Topmost = false;
    }

    internal void OnMainWindowHidden()
    {
        if (Settings.Current.TrayHintShown) return;
        Settings.Update(s => s.TrayHintShown = true);
        _tray?.ShowBalloon(Loc.T("Tray.StillRunning.Title"), Loc.F("Tray.StillRunning.Body", CurrentGesture.ToString()));
    }

    internal void ExitApp()
    {
        if (IsExiting) return;
        IsExiting = true;
        Settings.Save();
        _mainWindow?.Close();
        _osd?.Close();
        Shutdown();
    }

    // ------------------------------------------------------------------ shortcut

    internal bool TrySetHotkey(HotkeyGesture gesture, out string? error)
    {
        error = null;
        if (!gesture.IsValid)
        {
            error = Loc.T("Settings.Shortcut.Invalid");
            return false;
        }
        if (_hotkeys is null) return false;
        if (!_hotkeys.Register(gesture))
        {
            error = Loc.T("Settings.Shortcut.InUse");
            if (!_suspendedForCapture) RegisterCurrentHotkey();
            return false;
        }
        CurrentGesture = gesture;
        HotkeyRegistrationFailed = false;
        _suspendedForCapture = false;
        Settings.Update(s =>
        {
            s.HotkeyModifiers = gesture.Modifiers.ToString();
            s.HotkeyKey = gesture.Key.ToString();
        });
        if (_paused) _hotkeys.Unregister();
        RefreshTray();
        RaiseStatusChanged();
        return true;
    }

    /// <summary>While the user records a new shortcut, the old one must not fire.</summary>
    internal void SuspendHotkeyForCapture()
    {
        _suspendedForCapture = true;
        _hotkeys?.Unregister();
    }

    internal void ResumeHotkeyAfterCapture()
    {
        if (!_suspendedForCapture) return;
        _suspendedForCapture = false;
        RegisterCurrentHotkey();
    }

    internal void SetPaused(bool paused)
    {
        _paused = paused;
        if (paused) _hotkeys?.Unregister();
        else RegisterCurrentHotkey();
        RefreshTray();
        RaiseStatusChanged();
    }

    private void RegisterCurrentHotkey()
    {
        if (_hotkeys is null || _paused || _suspendedForCapture) return;
        HotkeyRegistrationFailed = !_hotkeys.Register(CurrentGesture);
        RaiseStatusChanged();
    }

    private async void OnHotkeyPressed(object? sender, EventArgs e)
    {
        if (_fixer is null) return;
        try
        {
            FixOutcome outcome = await _fixer.FixSelectionAsync(CurrentGesture);
            Report(outcome);
        }
        catch (Exception ex)
        {
            Logger.Error("The shortcut handler failed.", ex);
        }
    }

    private void Report(FixOutcome outcome)
    {
        string? message = null;
        bool success = false;
        bool notify = Settings.Current.ShowNotifications;
        switch (outcome.Kind)
        {
            case FixKind.Converted:
                success = true;
                Settings.Update(s => s.FixCount++);
                if (notify) message = Loc.T(outcome.Direction == ConversionDirection.LatinToArabic ? "Osd.ToArabic" : "Osd.ToLatin");
                MaybeAskForRating();
                break;
            case FixKind.Reverted:
                success = true;
                if (notify) message = Loc.T("Osd.Reverted");
                break;
            case FixKind.NothingSelected:
                message = Loc.T("Osd.NothingSelected");
                break;
            case FixKind.NothingToConvert:
                message = Loc.T("Osd.NothingToConvert");
                break;
            case FixKind.ElevatedTarget:
                message = Loc.T("Osd.Elevated");
                break;
            case FixKind.Terminal:
                message = Loc.T("Osd.Terminal");
                break;
            case FixKind.Failed:
                message = Loc.T("Osd.Failed");
                break;
        }
        if (message is not null) _osd?.ShowMessage(message, success, outcome.Foreground);
    }

    // ------------------------------------------------------------------ tray actions

    private async Task FixClipboardAsync()
    {
        try
        {
            string? text = await ClipboardService.TryGetTextAsync();
            if (string.IsNullOrEmpty(text))
            {
                _osd?.ShowMessage(Loc.T("Osd.ClipboardEmpty"), false, IntPtr.Zero);
                return;
            }
            ConversionResult result = Layouts.GetActivePair().Converter.Convert(text);
            if (!result.Changed)
            {
                _osd?.ShowMessage(Loc.T("Osd.NothingToConvert"), false, IntPtr.Zero);
                return;
            }
            await ClipboardService.SetTextAsync(result.Text, keepOutOfHistory: false);
            _osd?.ShowMessage(Loc.T("Osd.ClipboardFixed"), true, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            Logger.Error("Fixing the clipboard failed.", ex);
            _osd?.ShowMessage(Loc.T("Osd.Failed"), false, IntPtr.Zero);
        }
    }

    private void MaybeAskForRating()
    {
        AppSettings s = Settings.Current;
        if (s.RatingPromptShown || s.FixCount < RatingPromptAfterFixes || !PackageInfo.IsPackaged) return;
        Settings.Update(x => x.RatingPromptShown = true);
        _ratingBalloonPending = true;
        _tray?.ShowBalloon(Loc.T("Rate.Title"), Loc.F("Rate.Body", s.FixCount));
    }

    private async void OnTrayBalloonClicked(object? sender, EventArgs e)
    {
        if (!_ratingBalloonPending)
        {
            ShowMainWindow();
            return;
        }
        _ratingBalloonPending = false;
        ShowMainWindow(MainWindowTab.About);
        if (_mainWindow is not null) await StoreService.RequestRateAndReviewAsync(_mainWindow);
    }

    // ------------------------------------------------------------------ language and theme

    internal void ChangeLanguage(string language)
    {
        Settings.Update(s => s.Language = language);
        Loc.Apply(language);
        RefreshTray();
    }

    internal void ChangeTheme(string theme)
    {
        Settings.Update(s => s.Theme = theme);
        bool dark = ThemeService.IsDark(theme);
        ThemeService.ApplyBrushes(dark);
        _mainWindow?.ApplyTheme();
        _tray?.ApplyTheme(dark);
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle)) return;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (Settings.Current.Theme != "System") return;
            bool dark = ThemeService.SystemUsesDarkTheme();
            ThemeService.ApplyBrushes(dark);
            _tray?.ApplyTheme(dark);
        }));
    }

    private void RefreshTray() =>
        _tray?.Update(Loc.IsRtl, _paused, CurrentGesture.ToString(), ThemeService.IsDark(Settings.Current.Theme));

    private void RaiseStatusChanged() => StatusChanged?.Invoke(this, EventArgs.Empty);

    private void RegisterErrorHandlers()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            Logger.Error("Unhandled UI exception.", args.Exception);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Logger.Error("Unhandled exception.", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Logger.Error("Unobserved task exception.", args.Exception);
            args.SetObserved();
        };
    }
}
