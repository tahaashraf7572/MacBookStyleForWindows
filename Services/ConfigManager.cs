using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MacBookStyleForWindows.Services;

public class AppSettings
{
    // Appearance
    public string Theme { get; set; } = "macOS Dark";
    public bool TransparencyEnabled { get; set; } = true;
    public bool AnimationsEnabled { get; set; } = true;
    public string? WallpaperPath { get; set; }
    public string AccentColor { get; set; } = "#0A84FF";

    // Dock
    public string DockPosition { get; set; } = "Bottom"; // Bottom / Left / Right
    public double DockIconSize { get; set; } = 52;
    public double DockMagnification { get; set; } = 1.6;
    public bool DockAutoHide { get; set; } = false;
    public System.Collections.Generic.List<string> PinnedApps { get; set; } = new();

    // Menu Bar
    public bool MenuBarEnabled { get; set; } = true;
    public bool ShowWifi { get; set; } = true;
    public bool ShowBattery { get; set; } = true;
    public bool ShowVolume { get; set; } = true;
    public bool ShowClock { get; set; } = true;

    // Performance / Startup
    public string AnimationQualityOverride { get; set; } = "Auto"; // Auto / Full / Reduced / Minimal
    public bool StartWithWindows { get; set; } = true;

    // Windows
    public bool RoundedWindowCorners { get; set; } = true;
    public bool TrafficLightButtons { get; set; } = false; // opt-in, off by default (see README)
}

/// <summary>
/// Loads settings synchronously once (fast: small JSON file, happens before UI shows).
/// All subsequent writes are debounced and pushed to a background thread so typing/dragging
/// sliders in Settings never causes disk-write stutter.
/// </summary>
public static class ConfigManager
{
    private static readonly string ConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MacBookStyleForWindows");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "settings.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static Timer? _debounceTimer;
    private static readonly object Lock = new();

    public static AppSettings Current { get; private set; } = new();

    public static void Load()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                Current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            Current = new AppSettings(); // corrupt config never crashes the app
        }
    }

    /// <summary>Call after mutating Current.* — writes are coalesced to at most once per 400ms.</summary>
    public static void SaveDebounced()
    {
        lock (Lock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(_ => SaveNow(), null, 400, Timeout.Infinite);
        }
    }

    public static void SaveNow()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var json = JsonSerializer.Serialize(Current, JsonOptions);
            File.WriteAllText(ConfigPath, json);
        }
        catch
        {
            // Best-effort; a failed save should never crash the app.
        }
    }
}
