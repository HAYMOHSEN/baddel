using System;
using System.Drawing;
using WinForms = System.Windows.Forms;

namespace Baddel.Services;

/// <summary>The icon next to the clock, with its menu.</summary>
internal sealed class TrayIconService : IDisposable
{
    private readonly WinForms.NotifyIcon _icon;
    private readonly WinForms.ContextMenuStrip _menu;
    private readonly WinForms.ToolStripMenuItem _open;
    private readonly WinForms.ToolStripMenuItem _fixClipboard;
    private readonly WinForms.ToolStripMenuItem _pause;
    private readonly WinForms.ToolStripMenuItem _settings;
    private readonly WinForms.ToolStripMenuItem _exit;

    public TrayIconService()
    {
        _open = Item(() => OpenRequested?.Invoke(this, EventArgs.Empty));
        _open.Font = new Font("Segoe UI", 9.75f, FontStyle.Bold);
        _fixClipboard = Item(() => FixClipboardRequested?.Invoke(this, EventArgs.Empty));
        _pause = Item(() => PauseToggled?.Invoke(this, EventArgs.Empty));
        _settings = Item(() => SettingsRequested?.Invoke(this, EventArgs.Empty));
        _exit = Item(() => ExitRequested?.Invoke(this, EventArgs.Empty));

        _menu = new WinForms.ContextMenuStrip
        {
            ShowImageMargin = false,
            Font = new Font("Segoe UI", 9.75f),
            Padding = new WinForms.Padding(4),
        };
        _menu.Items.AddRange(new WinForms.ToolStripItem[]
        {
            _open, new WinForms.ToolStripSeparator(), _fixClipboard, _pause, _settings, new WinForms.ToolStripSeparator(), _exit,
        });

        _icon = new WinForms.NotifyIcon { Icon = LoadIcon(), ContextMenuStrip = _menu, Text = "Baddel" };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left) OpenRequested?.Invoke(this, EventArgs.Empty);
        };
        _icon.BalloonTipClicked += (_, _) => BalloonClicked?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? OpenRequested;
    public event EventHandler? FixClipboardRequested;
    public event EventHandler? PauseToggled;
    public event EventHandler? SettingsRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler? BalloonClicked;

    public void Show() => _icon.Visible = true;

    public void Update(bool rightToLeft, bool paused, string shortcut, bool dark)
    {
        _menu.RightToLeft = rightToLeft ? WinForms.RightToLeft.Yes : WinForms.RightToLeft.No;
        _open.Text = Loc.T("Tray.Open");
        _fixClipboard.Text = Loc.T("Tray.FixClipboard");
        _pause.Text = Loc.T(paused ? "Tray.Resume" : "Tray.Pause");
        _settings.Text = Loc.T("Tray.Settings");
        _exit.Text = Loc.T("Tray.Exit");
        string tip = paused ? Loc.T("Tray.TooltipPaused") : Loc.F("Tray.Tooltip", shortcut);
        _icon.Text = tip.Length > 120 ? tip.Substring(0, 120) : tip;
        ApplyTheme(dark);
    }

    public void ApplyTheme(bool dark) => _menu.Renderer = new MenuRenderer(dark);

    public void ShowBalloon(string title, string text) => _icon.ShowBalloonTip(5000, title, text, WinForms.ToolTipIcon.None);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }

    private static WinForms.ToolStripMenuItem Item(Action onClick)
    {
        var item = new WinForms.ToolStripMenuItem { Padding = new WinForms.Padding(6, 4, 6, 4) };
        item.Click += (_, _) => onClick();
        return item;
    }

    private static Icon LoadIcon()
    {
        var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/Baddel.ico", UriKind.Absolute));
        if (resource is null) return SystemIcons.Application;
        using var stream = resource.Stream;
        return new Icon(stream, WinForms.SystemInformation.SmallIconSize);
    }

    private sealed class MenuRenderer : WinForms.ToolStripProfessionalRenderer
    {
        private readonly bool _dark;

        public MenuRenderer(bool dark) : base(new MenuColors(dark))
        {
            _dark = dark;
            RoundedEdges = false;
        }

        protected override void OnRenderItemText(WinForms.ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = !e.Item.Enabled ? Color.Gray : _dark ? Color.FromArgb(240, 240, 240) : Color.FromArgb(26, 26, 26);
            base.OnRenderItemText(e);
        }
    }

    private sealed class MenuColors : WinForms.ProfessionalColorTable
    {
        private readonly Color _back;
        private readonly Color _hover;
        private readonly Color _border;

        public MenuColors(bool dark)
        {
            UseSystemColors = false;
            _back = dark ? Color.FromArgb(44, 44, 44) : Color.FromArgb(249, 249, 249);
            _hover = dark ? Color.FromArgb(64, 64, 64) : Color.FromArgb(232, 232, 232);
            _border = dark ? Color.FromArgb(70, 70, 70) : Color.FromArgb(214, 214, 214);
        }

        public override Color ToolStripDropDownBackground => _back;
        public override Color MenuBorder => _border;
        public override Color MenuItemBorder => _hover;
        public override Color MenuItemSelected => _hover;
        public override Color MenuItemSelectedGradientBegin => _hover;
        public override Color MenuItemSelectedGradientEnd => _hover;
        public override Color ImageMarginGradientBegin => _back;
        public override Color ImageMarginGradientMiddle => _back;
        public override Color ImageMarginGradientEnd => _back;
        public override Color SeparatorDark => _border;
        public override Color SeparatorLight => _back;
    }
}
