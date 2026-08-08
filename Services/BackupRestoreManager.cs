using Microsoft.Win32;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace MacBookStyleForWindows.Services;

internal class BackupSnapshot
{
    public string? OriginalWallpaper { get; set; }
    public int OriginalWallpaperStyle { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// This app never edits system files. The only "system" state it can touch is the
/// current wallpaper (via the public SystemParametersInfo API) and its own HKCU Run entry.
/// Both are snapshotted here before the first customization so RestoreOriginal() can undo them.
/// </summary>
public static class BackupRestoreManager
{
    private static readonly string BackupPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MacBookStyleForWindows", "original-state.json");

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SystemParametersInfo(uint uAction, uint uParam, string lpvParam, uint fuWinIni);

    private const uint SPI_SETDESKWALLPAPER = 0x0014;
    private const uint SPIF_UPDATEINIFILE = 0x01;
    private const uint SPIF_SENDCHANGE = 0x02;

    public static void CreateBackupIfMissing()
    {
        if (File.Exists(BackupPath)) return;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            var wallpaper = key?.GetValue("WallPaper") as string;
            var style = key?.GetValue("WallpaperStyle") as string;

            var snapshot = new BackupSnapshot
            {
                OriginalWallpaper = wallpaper,
                OriginalWallpaperStyle = int.TryParse(style, out var s) ? s : 10,
                CreatedAt = DateTime.Now
            };

            Directory.CreateDirectory(Path.GetDirectoryName(BackupPath)!);
            File.WriteAllText(BackupPath, JsonSerializer.Serialize(snapshot));
        }
        catch
        {
            // Non-fatal — restore will simply be unavailable for wallpaper if this fails.
        }
    }

    public static void RestoreOriginal()
    {
        // 1. Restore original wallpaper if we have one saved.
        try
        {
            if (File.Exists(BackupPath))
            {
                var snapshot = JsonSerializer.Deserialize<BackupSnapshot>(File.ReadAllText(BackupPath));
                if (!string.IsNullOrEmpty(snapshot?.OriginalWallpaper) && File.Exists(snapshot.OriginalWallpaper))
                {
                    SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, snapshot.OriginalWallpaper,
                        SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                }
            }
        }
        catch { /* best-effort */ }

        // 2. Remove startup entry.
        StartupManager.SetEnabled(false);

        // 3. Reset app settings to defaults (turns off dock/menu bar/theme on next launch).
        ConfigManager.Current = new AppSettings
        {
            MenuBarEnabled = false,
            DockAutoHide = true,
            StartWithWindows = false
        };
        ConfigManager.SaveNow();
    }
}
