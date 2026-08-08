using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MacBookStyleForWindows.Services;

public record AppEntry(string Name, string LaunchPath, string IconPath);

/// <summary>
/// Indexes Start Menu shortcuts (per-user + all-users) exactly once at startup and caches
/// the result in memory. Re-scanning only happens if the user explicitly refreshes from
/// Settings, keeping Spotlight's search box instant (in-memory string match, no disk I/O per keystroke).
/// </summary>
public static class AppLauncher
{
    private static List<AppEntry> _cache = new();
    public static bool IsIndexed { get; private set; }

    private static readonly string[] ShortcutRoots =
    {
        Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
        Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
    };

    public static async Task IndexAsync()
    {
        _cache = await Task.Run(() =>
        {
            var results = new List<AppEntry>();
            foreach (var root in ShortcutRoots)
            {
                if (!Directory.Exists(root)) continue;
                try
                {
                    foreach (var file in Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories))
                    {
                        var name = Path.GetFileNameWithoutExtension(file);
                        // Skip uninstallers / help links noise commonly found in Start Menu folders.
                        if (name.Contains("uninstall", StringComparison.OrdinalIgnoreCase)) continue;
                        results.Add(new AppEntry(name, file, file));
                    }
                }
                catch { /* locked/permission-restricted folders are skipped, not fatal */ }
            }
            return results
                .GroupBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(a => a.Name)
                .ToList();
        });
        IsIndexed = true;
    }

    /// <summary>Fast in-memory substring + prefix-ranked search — no disk access, sub-millisecond for typical Start Menu sizes.</summary>
    public static IReadOnlyList<AppEntry> Search(string query, int maxResults = 8)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<AppEntry>();

        return _cache
            .Where(a => a.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => a.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(a => a.Name.Length)
            .Take(maxResults)
            .ToList();
    }

    public static IReadOnlyList<AppEntry> AllApps => _cache;

    public static void Launch(AppEntry entry)
    {
        try
        {
            Process.Start(new ProcessStartInfo(entry.LaunchPath) { UseShellExecute = true });
        }
        catch { /* silently ignore broken shortcuts rather than crashing the dock/search */ }
    }
}
