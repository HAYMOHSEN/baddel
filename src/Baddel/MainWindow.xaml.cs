using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Threading;
using Baddel.Core;
using Baddel.Services;

namespace Baddel;

internal enum MainWindowTab
{
    Converter,
    Settings,
    About,
}

/// <summary>An entry in the keyboard-layout lists.</summary>
public sealed record LayoutChoice(string Label, string? Id)
{
    public override string ToString() => Label;
}

public partial class MainWindow : Window
{
    private readonly App _app;
    private readonly DispatcherTimer _copyStatusTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool _loading;

    internal MainWindow(App app)
    {
        _app = app;
        InitializeComponent();
        ApplyTheme();

        Loc.LanguageChanged += OnLanguageApplied;
        _app.StatusChanged += OnAppStatusChanged;
        Closing += OnClosing;
        Closed += (_, _) =>
        {
            Loc.LanguageChanged -= OnLanguageApplied;
            _app.StatusChanged -= OnAppStatusChanged;
        };
        InputBox.TextChanged += (_, _) => RefreshConversion();
        DirectionCombo.SelectionChanged += (_, _) => RefreshConversion();
        ShortcutBox.CaptureStarted += (_, _) => _app.SuspendHotkeyForCapture();
        ShortcutBox.CaptureEnded += (_, _) =>
        {
            _app.ResumeHotkeyAfterCapture();
            ShowCurrentShortcut();
        };
        ShortcutBox.GestureCaptured += OnGestureCaptured;
        _copyStatusTimer.Tick += (_, _) =>
        {
            _copyStatusTimer.Stop();
            CopyStatus.Visibility = Visibility.Collapsed;
        };

        LoadSettingsIntoUi();
        ApplyLanguage();
        Loaded += async (_, _) => await RefreshStartupStateAsync();
    }

    internal void SelectTab(MainWindowTab tab) => Tabs.SelectedItem = tab switch
    {
        MainWindowTab.Settings => SettingsTab,
        MainWindowTab.About => AboutTab,
        _ => ConverterTab,
    };

    internal void ApplyTheme()
    {
        try
        {
            ThemeMode = ThemeService.ToThemeMode(_app.Settings.Current.Theme);
        }
        catch (Exception ex)
        {
            Logger.Error("The Fluent theme could not be applied.", ex);
        }
    }

    // ------------------------------------------------------------------ language and status

    private void ApplyLanguage()
    {
        FlowDirection = Loc.Flow;
        ShowCurrentShortcut();
        UpdateStatus();
        VersionText.Text = Loc.F("About.Version", PackageInfo.DisplayVersion);
        DeveloperText.Text = Loc.F("About.Developer", AppInfo.DeveloperName);
        PopulateLayoutChoices();
        RefreshConversion();
    }

    private void OnLanguageApplied(object? sender, EventArgs e) => ApplyLanguage();

    private void OnAppStatusChanged(object? sender, EventArgs e)
    {
        ShowCurrentShortcut();
        UpdateStatus();
    }

    private void ShowCurrentShortcut()
    {
        ShortcutKeys.ItemsSource = _app.CurrentGesture.KeyLabels;
        WelcomeTryText.Text = Loc.F("Welcome.Try", _app.CurrentGesture.ToString());
        if (!ShortcutBox.IsKeyboardFocused) ShortcutBox.Text = _app.CurrentGesture.ToString();
    }

    private void UpdateStatus()
    {
        bool failed = _app.HotkeyRegistrationFailed && !_app.IsPaused;
        string key = _app.IsPaused ? "Status.Paused" : failed ? "Status.NoShortcut" : "Status.Running";
        StatusText.Text = Loc.T(key);
        StatusDot.SetResourceReference(Shape.FillProperty, _app.IsPaused || failed ? "Baddel.Caution" : "Baddel.Success");
        ShortcutBanner.Visibility = failed ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnOpenShortcutSettings(object sender, RoutedEventArgs e)
    {
        SelectTab(MainWindowTab.Settings);
        ShortcutBox.Focus();
    }

    // ------------------------------------------------------------------ settings

    private void LoadSettingsIntoUi()
    {
        _loading = true;
        AppSettings s = _app.Settings.Current;
        SelectLineCheck.IsChecked = s.SelectLineWhenNothingSelected;
        SwitchLayoutCheck.IsChecked = s.SwitchKeyboardLayout;
        RestoreClipboardCheck.IsChecked = s.RestoreClipboard;
        NotificationsCheck.IsChecked = s.ShowNotifications;
        LanguageCombo.SelectedIndex = s.Language == "en" ? 1 : 0;
        ThemeCombo.SelectedIndex = s.Theme switch
        {
            "Light" => 1,
            "Dark" => 2,
            _ => 0,
        };
        WelcomeCard.Visibility = s.FirstRunCompleted ? Visibility.Collapsed : Visibility.Visible;
        _loading = false;
    }

    private void OnBehaviorChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _app.Settings.Update(s =>
        {
            s.SelectLineWhenNothingSelected = SelectLineCheck.IsChecked == true;
            s.SwitchKeyboardLayout = SwitchLayoutCheck.IsChecked == true;
            s.RestoreClipboard = RestoreClipboardCheck.IsChecked == true;
            s.ShowNotifications = NotificationsCheck.IsChecked == true;
        });
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || LanguageCombo.SelectedItem is not ComboBoxItem { Tag: string language }) return;
        if (language == _app.Settings.Current.Language) return;
        _app.ChangeLanguage(language);
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ThemeCombo.SelectedItem is not ComboBoxItem { Tag: string theme }) return;
        if (theme == _app.Settings.Current.Theme) return;
        _app.ChangeTheme(theme);
    }

    private void PopulateLayoutChoices()
    {
        bool wasLoading = _loading;
        _loading = true;
        try
        {
            IReadOnlyList<InstalledLayout> layouts = KeyboardLayoutService.GetInstalledLayouts();
            AppSettings s = _app.Settings.Current;
            Fill(ArabicLayoutCombo, layouts.Where(l => l.IsArabic), s.ArabicLayout);
            Fill(LatinLayoutCombo, layouts.Where(l => !l.IsArabic), s.LatinLayout);
            LayoutNote.Text = Loc.T("Settings.Layouts.Missing");
            LayoutNote.Visibility = layouts.Any(l => l.IsArabic) ? Visibility.Collapsed : Visibility.Visible;
        }
        finally
        {
            _loading = wasLoading;
        }

        static void Fill(ComboBox combo, IEnumerable<InstalledLayout> options, string? selectedId)
        {
            var items = new List<LayoutChoice> { new(Loc.T("Settings.Layouts.Auto"), null) };
            items.AddRange(options.Select(l => new LayoutChoice(l.DisplayName, l.Id)));
            combo.ItemsSource = items;
            combo.SelectedItem = items.FirstOrDefault(i => i.Id == selectedId) ?? items[0];
        }
    }

    private void OnLayoutChoiceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        string? arabic = (ArabicLayoutCombo.SelectedItem as LayoutChoice)?.Id;
        string? latin = (LatinLayoutCombo.SelectedItem as LayoutChoice)?.Id;
        _app.Settings.Update(s =>
        {
            s.ArabicLayout = arabic;
            s.LatinLayout = latin;
        });
        _app.Layouts.Invalidate();
        RefreshConversion();
    }

    // ------------------------------------------------------------------ shortcut

    private void OnGestureCaptured(object? sender, HotkeyGesture gesture)
    {
        if (_app.TrySetHotkey(gesture, out string? error))
        {
            ShowShortcutStatus(Loc.F("Settings.Shortcut.Saved", gesture.ToString()), isError: false);
            ShortcutBox.MoveFocusAway();
        }
        else
        {
            ShortcutBox.Text = gesture.ToString();
            ShowShortcutStatus(error ?? Loc.T("Settings.Shortcut.InUse"), isError: true);
        }
    }

    private void OnResetShortcut(object sender, RoutedEventArgs e)
    {
        bool ok = _app.TrySetHotkey(HotkeyGesture.Default, out string? error);
        ShowCurrentShortcut();
        ShowShortcutStatus(ok ? Loc.F("Settings.Shortcut.Saved", HotkeyGesture.Default.ToString()) : error ?? string.Empty, !ok);
    }

    private void ShowShortcutStatus(string message, bool isError)
    {
        ShortcutStatus.Text = message;
        ShortcutStatus.SetResourceReference(TextBlock.ForegroundProperty, isError ? "Baddel.Caution" : "Baddel.TextSecondary");
        ShortcutStatus.Visibility = Visibility.Visible;
    }

    // ------------------------------------------------------------------ start with Windows

    private async Task RefreshStartupStateAsync() => ShowStartupState(await StartupService.GetStateAsync());

    private void ShowStartupState(StartupState state)
    {
        _loading = true;
        StartupCheck.IsChecked = state == StartupState.Enabled;
        StartupCheck.IsEnabled = state is not (StartupState.DisabledByPolicy or StartupState.Unavailable);
        _loading = false;
        string? note = state switch
        {
            StartupState.DisabledByUser => Loc.T("Settings.Startup.DisabledByUser"),
            StartupState.DisabledByPolicy => Loc.T("Settings.Startup.DisabledByPolicy"),
            StartupState.Unavailable => Loc.T("Settings.Startup.Unavailable"),
            _ => null,
        };
        StartupNote.Text = note ?? string.Empty;
        StartupNote.Visibility = note is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void OnStartupClicked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        ShowStartupState(await StartupService.SetEnabledAsync(StartupCheck.IsChecked == true));
    }

    private async void OnWelcomeDone(object sender, RoutedEventArgs e)
    {
        WelcomeCard.Visibility = Visibility.Collapsed;
        _app.Settings.Update(s => s.FirstRunCompleted = true);
        if (WelcomeStartupCheck.IsChecked == true)
            ShowStartupState(await StartupService.SetEnabledAsync(true));
    }

    // ------------------------------------------------------------------ converter

    private void RefreshConversion()
    {
        string text = InputBox.Text;
        InputHint.Visibility = text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (text.Length == 0)
        {
            OutputBox.Text = string.Empty;
            return;
        }
        LayoutConverter converter = _app.Layouts.GetActivePair().Converter;
        ConversionDirection? forced = ((DirectionCombo.SelectedItem as ComboBoxItem)?.Tag as string) switch
        {
            "ToArabic" => ConversionDirection.LatinToArabic,
            "ToLatin" => ConversionDirection.ArabicToLatin,
            _ => null,
        };
        ConversionResult result = converter.Convert(text, new ConversionOptions { ForcedDirection = forced });
        OutputBox.Text = result.Text;
        InputBox.FlowDirection = converter.DetectDirection(text) == ConversionDirection.ArabicToLatin
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
        OutputBox.FlowDirection = result.Direction == ConversionDirection.LatinToArabic
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
    }

    private void OnSwap(object sender, RoutedEventArgs e)
    {
        InputBox.Text = OutputBox.Text;
        InputBox.Focus();
        InputBox.CaretIndex = InputBox.Text.Length;
    }

    private void OnClear(object sender, RoutedEventArgs e)
    {
        InputBox.Clear();
        InputBox.Focus();
    }

    private async void OnCopy(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(OutputBox.Text)) return;
        try
        {
            await ClipboardService.SetTextAsync(OutputBox.Text, keepOutOfHistory: false);
            CopyStatus.Text = Loc.T("Convert.Copied");
            CopyStatus.Visibility = Visibility.Visible;
            _copyStatusTimer.Stop();
            _copyStatusTimer.Start();
        }
        catch (Exception ex)
        {
            Logger.Error("Copying the result failed.", ex);
        }
    }

    // ------------------------------------------------------------------ about

    private async void OnRate(object sender, RoutedEventArgs e) => await StoreService.RequestRateAndReviewAsync(this);

    private void OnContactSupport(object sender, RoutedEventArgs e) =>
        StoreService.OpenUri($"mailto:{AppInfo.SupportEmail}?subject={Uri.EscapeDataString("Baddel " + PackageInfo.DisplayVersion)}");

    private void OnOpenPrivacy(object sender, RoutedEventArgs e) => StoreService.OpenUri(AppInfo.PrivacyPolicyUrl);

    private void OnOpenNotices(object sender, RoutedEventArgs e)
    {
        string path = System.IO.Path.Combine(AppContext.BaseDirectory, "THIRD-PARTY-NOTICES.txt");
        if (File.Exists(path)) StoreService.OpenUri(path);
    }

    // ------------------------------------------------------------------ window

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_app.IsExiting) return;
        e.Cancel = true; // closing the window keeps Baddel running next to the clock
        Hide();
        _app.OnMainWindowHidden();
    }
}
