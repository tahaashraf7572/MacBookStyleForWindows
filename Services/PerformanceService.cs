using System;
using System.Threading.Tasks;
using System.Windows.Media;

namespace MacBookStyleForWindows.Services;

public enum AnimationQuality
{
    Full,       // High-end: blur, magnification, all transitions
    Reduced,    // Mid-range: simple fades only, no live blur
    Minimal     // Low-end / battery saver: instant, no animation, no transparency
}

/// <summary>
/// Detects GPU render tier + logical core count ONCE at startup on a background thread,
/// then caches the result. Nothing in this class ever polls in a loop, so idle CPU stays ~0%.
/// All animated components (Dock, Spotlight, MenuBar) read PerformanceService.Quality once
/// and branch their storyboard construction accordingly.
/// </summary>
public static class PerformanceService
{
    public static AnimationQuality Quality { get; private set; } = AnimationQuality.Full;
    public static bool IsInitialized { get; private set; }

    /// <summary>Call once from App.OnStartup. Cheap (&lt;50ms typical) and fully async so it never blocks the UI thread.</summary>
    public static async Task InitializeAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                // RenderCapability.Tier: 0 = software rendering, 1 = partial HW, 2 = full HW acceleration.
                int tier = RenderCapability.Tier >> 16;
                int cores = Environment.ProcessorCount;
                bool onBattery = IsOnBattery();

                if (tier < 1 || cores <= 2)
                {
                    Quality = AnimationQuality.Minimal;
                }
                else if (tier < 2 || (onBattery && cores <= 4))
                {
                    Quality = AnimationQuality.Reduced;
                }
                else
                {
                    Quality = AnimationQuality.Full;
                }
            }
            catch
            {
                // Never let detection failure crash startup — degrade gracefully.
                Quality = AnimationQuality.Reduced;
            }
            finally
            {
                IsInitialized = true;
            }
        });
    }

    /// <summary>Animation durations scale down automatically on weaker hardware instead of being skipped entirely.</summary>
    public static TimeSpan ScaledDuration(TimeSpan full) => Quality switch
    {
        AnimationQuality.Full => full,
        AnimationQuality.Reduced => TimeSpan.FromTicks(full.Ticks / 2),
        _ => TimeSpan.Zero
    };

    public static bool BlurEnabled => Quality == AnimationQuality.Full;
    public static bool MagnificationEnabled => Quality != AnimationQuality.Minimal;

    // Lightweight native battery check (no WinForms dependency, avoids extra assembly load at startup).
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);

    private static bool IsOnBattery()
    {
        try
        {
            return GetSystemPowerStatus(out var status) && status.ACLineStatus == 0;
        }
        catch
        {
            return false;
        }
    }
}
