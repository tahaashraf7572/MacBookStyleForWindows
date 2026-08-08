using MacBookStyleForWindows.Dock;
using MacBookStyleForWindows.Search;
using MacBookStyleForWindows.Services;
using MacBookStyleForWindows.Settings;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Application = System.Windows.Application;

namespace MacBookStyleForWindows;

public partial class App : Application
{
    public static DockWindow? Dock { get; private set; }
    public static SpotlightWindow? Spotlight { get; private set; }

    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private const int HotkeyId = 0x0001;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ConfigManager.Load();
        BackupRestoreManager.CreateBackupIfMissing();

        if (e.Args.Contains("--restore-and-exit"))
        {
            BackupRestoreManager.RestoreOriginal();
            Shutdown();
            return;
        }

        var perfTask = PerformanceService.InitializeAsync();
        var indexTask = AppLauncher.IndexAsync();

        Dock = new DockWindow();
        Dock.Show();

        SetupTrayIcon();
        SetupGlobalHotkey();

        await Task.WhenAll(perfTask, indexTask);

        Dock.ApplyPerformanceProfile();
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true,
            Text = "MacBook Style for Windows"
        };

        var menu = new System.Windows.Forms.ContextMenuStrip();

        menu.Items.Add(
            "Settings",
            null,
            (_, _) => OpenSettings()
        );

        menu.Items.Add(
            "Toggle Dock",
            null,
            (_, _) => Dock?.ToggleVisibility()
        );

        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        menu.Items.Add(
            "Restore Original Windows Look",
            null,
            (_, _) => RestoreOriginal()
        );

        menu.Items.Add(
            "Exit",
            null,
            (_, _) => Shutdown()
        );

        _trayIcon.ContextMenuStrip = menu;

        _trayIcon.DoubleClick += (_, _) => OpenSettings();
    }

    private void SetupGlobalHotkey()
    {
        var helper = new WindowInteropHelper(Dock!);

        helper.EnsureHandle();

        NativeMethods.RegisterHotKey(
            helper.Handle,
            HotkeyId,
            NativeMethods.MOD_CONTROL,
            NativeMethods.VK_SPACE
        );

        HwndSource.FromHwnd(helper.Handle)!.AddHook(WndProc);
    }

    private IntPtr WndProc(
        IntPtr hwnd,
        int msg,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY &&
            wParam.ToInt32() == HotkeyId)
        {
            ToggleSpotlight();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void ToggleSpotlight()
    {
        if (Spotlight is { IsVisible: true })
        {
            Spotlight.HideAnimated();
            return;
        }

        Spotlight ??= new SpotlightWindow();
        Spotlight.ShowCentered();
    }

    public void OpenSettings()
    {
        var window = new SettingsWindow();

        window.Show();
        window.Activate();
    }

    public void RestoreOriginal()
    {
        var result = MessageBox.Show(
            "This will restore your original Windows wallpaper, remove MacBook Style for Windows from startup, and reset all customizations. Continue?",
            "Restore Original Windows Appearance",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning
        );

        if (result != MessageBoxResult.Yes)
            return;

        BackupRestoreManager.RestoreOriginal();

        Dock?.Hide();

        MessageBox.Show(
            "Original Windows appearance restored. You can now uninstall the app from Settings > Apps if desired."
        );
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (Dock != null)
        {
            var helper = new WindowInteropHelper(Dock);

            NativeMethods.UnregisterHotKey(
                helper.Handle,
                HotkeyId
            );
        }

        _trayIcon?.Dispose();

        base.OnExit(e);
    }
}
