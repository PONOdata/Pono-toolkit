using LenovoLegionToolkit.Lib.Utils;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace LenovoLegionToolkit.WPF.Extensions;

public static class WindowExtensions
{
    private static readonly HWND HWND_TOPMOST = new HWND(-1);

    [DllImport("user32.dll", EntryPoint = "GetWindowBand")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowBand(IntPtr hWnd, out uint pdwBand);

    public static void EscalateZBand(this Window window)
    {
        if (PresentationSource.FromVisual(window) is not HwndSource source)
            return;

        var hwnd = (HWND)source.Handle;
        try
        {
            PInvoke.SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);

            if (GetWindowBand(source.Handle, out uint currentBand))
            {
                Log.Instance.Trace($"EscalateZBand executed for {window.GetType().Name}. Current Band: {currentBand}");
            }
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"Exception for HWND {hwnd}", ex);
        }
    }

    public static void SetClickThrough(this Window window, bool clickThrough)
    {
        if (PresentationSource.FromVisual(window) is not HwndSource source)
            return;

        var hwnd = (HWND)source.Handle;
        var extendedStyle = (WINDOW_EX_STYLE)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);

        extendedStyle |= WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_NOACTIVATE;

        if (clickThrough)
            extendedStyle |= WINDOW_EX_STYLE.WS_EX_TRANSPARENT;
        else
            extendedStyle &= ~WINDOW_EX_STYLE.WS_EX_TRANSPARENT;

        PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)extendedStyle);
    }

    public static void BringToForeground(this Window window)
    {
        window.ShowInTaskbar = true;

        if (window.WindowState == WindowState.Minimized || window.Visibility == Visibility.Hidden)
        {
            window.Show();
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();
    }
}
