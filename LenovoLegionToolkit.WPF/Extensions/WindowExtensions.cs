using LenovoLegionToolkit.Lib.Utils;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LenovoLegionToolkit.WPF.Extensions;

public static class WindowExtensions
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", EntryPoint = "GetWindowBand")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowBand(IntPtr hWnd, out uint pdwBand);

    public static void EscalateZBand(this Window window)
    {
        if (PresentationSource.FromVisual(window) is not HwndSource source)
            return;

        var hwnd = source.Handle;
        try
        {
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

            if (GetWindowBand(hwnd, out uint currentBand))
            {
                Log.Instance.Trace($"EscalateZBand executed for {window.GetType().Name}. Current Band: {currentBand}");
            }
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"Exception for HWND {hwnd:X}", ex);
        }
    }

    public static void SetClickThrough(this Window window, bool clickThrough)
    {
        if (PresentationSource.FromVisual(window) is not HwndSource source)
            return;

        var hwnd = source.Handle;
        var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

        extendedStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;

        if (clickThrough)
            extendedStyle |= WS_EX_TRANSPARENT;
        else
            extendedStyle &= ~WS_EX_TRANSPARENT;

        SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle);
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
