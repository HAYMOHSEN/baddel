using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Baddel.Services;
using WinForms = System.Windows.Forms;

namespace Baddel.Views;

/// <summary>A small notice near the bottom of the screen. It never takes the focus from the app you type in.</summary>
public partial class OsdWindow : Window
{
    private static readonly Brush SuccessBadge = Frozen(Color.FromRgb(0xE9, 0xA2, 0x3B));
    private static readonly Brush HintBadge = Frozen(Color.FromRgb(0x8A, 0x94, 0xA6));
    private readonly DispatcherTimer _hideTimer = new();

    public OsdWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            long style = Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE);
            Native.SetWindowLong(hwnd, Native.GWL_EXSTYLE, style | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW | Native.WS_EX_TRANSPARENT);
        };
        _hideTimer.Tick += (_, _) => FadeOut();
    }

    internal void ShowMessage(string message, bool success, IntPtr nearWindow)
    {
        _hideTimer.Stop();
        FlowDirection = Loc.Flow;
        MessageText.Text = message;
        BadgeText.Text = success ? "\u0628" : "!";
        Badge.Background = success ? SuccessBadge : HintBadge;
        if (!IsVisible) Show();
        UpdateLayout();
        PlaceNear(nearWindow);
        BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(120)));
        _hideTimer.Interval = TimeSpan.FromMilliseconds(success ? 1400 : 2800);
        _hideTimer.Start();
    }

    private void FadeOut()
    {
        _hideTimer.Stop();
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(260));
        fade.Completed += (_, _) =>
        {
            if (Opacity <= 0.01) Hide();
        };
        BeginAnimation(OpacityProperty, fade);
    }

    private void PlaceNear(IntPtr window)
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        if (window == IntPtr.Zero) window = Native.GetForegroundWindow();
        WinForms.Screen screen = window != IntPtr.Zero ? WinForms.Screen.FromHandle(window) : WinForms.Screen.PrimaryScreen!;
        System.Drawing.Rectangle area = screen.WorkingArea;
        if (!Native.GetWindowRect(hwnd, out Native.RECT rect)) return;
        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        double scale = VisualTreeHelper.GetDpi(this).DpiScaleY;
        int x = area.Left + (area.Width - width) / 2;
        int y = area.Bottom - height - (int)(48 * scale);
        Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, x, y, 0, 0, Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
    }

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
