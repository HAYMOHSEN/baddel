using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Baddel.Services;

namespace Baddel.Controls;

/// <summary>A read-only box that records the next key combination the user presses.</summary>
public sealed class HotkeyBox : TextBox
{
    public HotkeyBox()
    {
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        IsUndoEnabled = false;
        FlowDirection = FlowDirection.LeftToRight;
        HorizontalContentAlignment = HorizontalAlignment.Center;
        Cursor = Cursors.Hand;
        ContextMenu = null;
    }

    internal event EventHandler<HotkeyGesture>? GestureCaptured;

    public event EventHandler? CaptureStarted;

    public event EventHandler? CaptureEnded;

    public void MoveFocusAway()
    {
        DependencyObject scope = FocusManager.GetFocusScope(this);
        FocusManager.SetFocusedElement(scope, null);
        Keyboard.ClearFocus();
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        Text = Loc.T("Settings.Shortcut.Recording");
        CaptureStarted?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        CaptureEnded?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        Key key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            _ => e.Key,
        };
        ModifierKeys modifiers = Keyboard.Modifiers;
        if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)) modifiers |= ModifierKeys.Windows;

        if (key == Key.Tab && modifiers == ModifierKeys.None)
        {
            base.OnPreviewKeyDown(e); // keep keyboard navigation working
            return;
        }
        e.Handled = true;
        if (key == Key.Escape && modifiers == ModifierKeys.None)
        {
            MoveFocusAway();
            return;
        }
        if (HotkeyGesture.IsModifierKey(key))
        {
            Text = new HotkeyGesture(modifiers, Key.None) + " + …";
            return;
        }
        GestureCaptured?.Invoke(this, new HotkeyGesture(modifiers, key));
    }
}
