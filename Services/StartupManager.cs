using Microsoft.Win32;
using System;
using System.Diagnostics;

namespace MacBookStyleForWindows.Services;

/// <summary>
/// Uses the standard per-user Run key (HKCU) — the same mechanism Windows itself exposes
/// via Settings > Apps > Startup. No admin rights, no scheduled task, no service.
/// </summary>
public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MacBookStyleForWindows";

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key is null) return;

        if (enabled)
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
                key.SetValue(ValueName, $"\"{exePath}\" --silent", RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
        return key?.GetValue(ValueName) != null;
    }
}
