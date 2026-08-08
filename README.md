# MacBook Style for Windows

A Windows 10/11 desktop customization app that adds a macOS-inspired Dock, Spotlight-style
search (Ctrl+Space), and a Settings app for theming — without replacing Windows, without
touching system files, and fully reversible.

This build is a **performance-first core**: the Dock, Spotlight search, Settings, and the
backup/restore/startup system are fully implemented and tuned to be fast on real laptops.
The Finder-style file manager, Control Center, and top Menu Bar are stubbed as clearly
marked extension points (see "Extending" below) so the app stays lean rather than shipping
half-finished versions of everything at once.

## Why it's fast

| Technique | Where |
|---|---|
| Zero polling — magnification, auto-hide, and running-indicators are driven by `MouseMove`/`MouseEnter`/`MouseLeave` events, never a repeating timer | `Dock/DockWindow.xaml.cs` |
| Hardware-tier detection runs once, in the background, and caches the result | `Services/PerformanceService.cs` |
| Animation durations *scale down* on weaker hardware instead of janking or being abruptly disabled | `PerformanceService.ScaledDuration()` |
| Start Menu is indexed once into memory at startup; every keystroke in Spotlight is an in-memory string match, not a disk scan | `Services/AppLauncher.cs` |
| Spotlight search is debounced (80ms) so fast typing doesn't rebuild the result list every keystroke | `Search/SpotlightWindow.xaml.cs` |
| Settings writes are debounced (400ms) so dragging a slider never blocks on disk I/O | `Services/ConfigManager.cs` |
| `PublishReadyToRun` + `TieredPGO` + `InvariantGlobalization` in the csproj for fast cold start | `MacBookStyleForWindows.csproj` |
| The Dock window shows immediately at startup; hardware detection and app indexing happen in parallel afterward, not before | `App.xaml.cs` |

## Building

Requirements: Windows 10/11, [.NET 8 SDK](https://dotnet.microsoft.com/download), Visual Studio 2022 (or `dotnet` CLI).

```powershell
# From the project root
dotnet restore
dotnet build -c Release
```

To produce a self-contained, ReadyToRun published build (recommended for the installer):

```powershell
dotnet publish MacBookStyleForWindows.csproj -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -o publish
```

## Creating the installer

1. Install [Inno Setup 6](https://jrsoftware.org/isdl.php).
2. Run the publish command above so `publish/` exists next to `installer/`.
3. Open `installer/setup.iss` in Inno Setup and click **Compile**.
4. Output: `installer/output/MacBook Style for Windows Setup.exe`.

The installer runs **per-user, no admin rights required**, and includes an uninstall task
that restores your original wallpaper and removes the startup entry before deleting files.

## Installing

1. Double-click `MacBook Style for Windows Setup.exe`.
2. Accept the license / choose install location.
3. Optionally check "Create desktop shortcut" and "Start automatically when Windows starts".
4. Click Install, then Launch.

The Dock appears at the bottom of the screen. Press **Ctrl+Space** for Spotlight search.
Right-click the tray icon (bottom-right of your taskbar) for Settings, Restore, and Exit.

## Uninstalling

Use **Settings > Apps > Installed apps > MacBook Style for Windows > Uninstall**, or the
Start Menu shortcut created at install time. Uninstall automatically restores your original
wallpaper and removes the app from Windows startup — your Windows install is left exactly as
it was.

You can also do this manually at any time without uninstalling: tray icon → **Restore Original
Windows Appearance**.

## Safety notes

- No Windows system files are modified. The only OS-level state touched is the desktop
  wallpaper (via the public `SystemParametersInfo` API) and a single per-user `HKCU\...\Run`
  registry value for startup — both are snapshotted before first use and reversible.
- The Windows taskbar, Explorer, and Windows Defender are never disabled or replaced.
- No data is collected or sent anywhere; there is no network code in this app at all.
- All UI runs as a normal user-mode process — no drivers, no services, no elevation.

## Troubleshooting

**Dock doesn't appear after install.** Check the tray icon exists (bottom-right of taskbar,
may be under the `^` overflow arrow) and click "Toggle Dock". If it's still missing, your
GPU driver may be blocking DWM blur — open Settings > Appearance and disable transparency.

**Ctrl+Space doesn't open Spotlight.** Another app (often an IME or clipboard manager) may
already own that hotkey. This is a known Windows limitation for global hotkeys — pick a
different combination by editing `HotkeyId` registration in `App.xaml.cs` and rebuilding.

**Animations feel choppy on an old laptop.** Open Settings > Performance and set Animation
Quality to "Reduced" or "Minimal" manually — auto-detection is a heuristic and may be
conservative or generous depending on your GPU driver.

**I want my Windows desktop back immediately.** Tray icon → Restore Original Windows
Appearance. This works even without uninstalling.

## Extending

The architecture is intentionally modular so the remaining spec items (Finder-style file
manager, Control Center, top Menu Bar, window traffic-light buttons, trackpad gestures) can
be added as additional `Window` + `Service` pairs following the same pattern as `Dock/` and
`Search/`:

- New always-on-top glass panels: copy `Dock/DockWindow.xaml` as a template (transparent,
  `AllowsTransparency`, `ShowInTaskbar=False`, blur via `NativeMethods.EnableBlur`).
- New system data (Wi-Fi, Bluetooth, brightness): wrap the relevant Win32/WinRT API in its
  own `Services/*.cs` file, following `StartupManager.cs`'s pattern of a small static class.
- Always read `PerformanceService.Quality` before adding any animation or repeating timer —
  that's the single rule that keeps this app fast on low-end hardware.
