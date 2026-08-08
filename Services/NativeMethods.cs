using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MacBookStyleForWindows.Services;

/// <summary>
/// All P/Invoke calls are grouped here. Only safe, documented, user-mode Win32 APIs are used.
/// No system files are touched, no drivers, no kernel-mode calls.
/// </summary>
internal static class NativeMethods
{
    // ---- Global hotkey (Ctrl+Space for Spotlight) ----
    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public const uint MOD_CONTROL = 0x0002;
    public const uint VK_SPACE = 0x20;
    public const int WM_HOTKEY = 0x0312;

    // ---- DWM blur / acrylic (glass Dock/Menu Bar look), applied per-window only ----
    [StructLayout(LayoutKind.Sequential)]
    public struct DWM_BLURBEHIND
    {
        public uint dwFlags;
        public bool fEnable;
        public IntPtr hRgnBlur;
        public bool fTransitionOnMaximized;
    }

    [DllImport("dwmapi.dll")]
    public static extern int DwmEnableBlurBehindWindow(IntPtr hWnd, ref DWM_BLURBEHIND blurBehind);

    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    // DWMWA_WINDOW_CORNER_PREFERENCE (Windows 11 rounded corners)
    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    public const int DWMWCP_ROUND = 2;

    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int GWL_EXSTYLE = -20;

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    /// <summary>Enables a soft acrylic/blur background behind a WPF window with a transparent background.</summary>
    public static void EnableBlur(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var blur = new DWM_BLURBEHIND { dwFlags = 0x1 /* DWM_BB_ENABLE */, fEnable = true, hRgnBlur = IntPtr.Zero };
        DwmEnableBlurBehindWindow(hwnd, ref blur);
        int pref = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
    }

    /// <summary>Marks a window as a tool window that never appears in Alt+Tab or the taskbar.</summary>
    public static void MakeToolWindow(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        int style = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_TOOLWINDOW);
    }
}
