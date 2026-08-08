using MacBookStyleForWindows.Services;
using System;
using System.Windows;
using System.Windows.Controls;

namespace MacBookStyleForWindows.Settings;

public partial class SettingsWindow : Window
{
    private bool _loading = true;

    public SettingsWindow()
    {
        InitializeComponent();
        LoadFromConfig();
        WireChangeHandlers();
        _loading = false;

        DetectedTierText.Text = PerformanceService.IsInitialized
            ? $"Detected hardware profile: {PerformanceService.Quality}"
            : "Detecting hardware profile...";
    }

    private void LoadFromConfig()
    {
        var c = ConfigManager.Current;
        TransparencyCheck.IsChecked = c.TransparencyEnabled;
        AnimationsCheck.IsChecked = c.AnimationsEnabled;
        RoundedCornersCheck.IsChecked = c.RoundedWindowCorners;
        SelectComboByContent(ThemeCombo, c.Theme);

        SelectComboByContent(DockPositionCombo, c.DockPosition);
        DockSizeSlider.Value = c.DockIconSize;
        MagnificationSlider.Value = c.DockMagnification;
        AutoHideCheck.IsChecked = c.DockAutoHide;

        MenuBarEnabledCheck.IsChecked = c.MenuBarEnabled;
        ShowWifiCheck.IsChecked = c.ShowWifi;
        ShowBatteryCheck.IsChecked = c.ShowBattery;
        ShowVolumeCheck.IsChecked = c.ShowVolume;
        ShowClockCheck.IsChecked = c.ShowClock;

        AnimationQualityCombo.SelectedIndex = c.AnimationQualityOverride switch
        {
            "Full" => 1,
            "Reduced" => 2,
            "Minimal" => 3,
            _ => 0
        };

        StartWithWindowsCheck.IsChecked = c.StartWithWindows;
    }

    private static void SelectComboByContent(ComboBox combo, string value)
    {
        foreach (ComboBoxItem item in combo.Items)
        {
            if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = item;
                return;
            }
        }
        combo.SelectedIndex = 0;
    }

    private void WireChangeHandlers()
    {
        TransparencyCheck.Click += (_, _) => Save(c => c.TransparencyEnabled = TransparencyCheck.IsChecked == true);
        AnimationsCheck.Click += (_, _) => Save(c => c.AnimationsEnabled = AnimationsCheck.IsChecked == true);
        RoundedCornersCheck.Click += (_, _) => Save(c => c.RoundedWindowCorners = RoundedCornersCheck.IsChecked == true);
        ThemeCombo.SelectionChanged += (_, _) => Save(c => c.Theme = SelectedText(ThemeCombo));

        DockPositionCombo.SelectionChanged += (_, _) => Save(c => c.DockPosition = SelectedText(DockPositionCombo));
        DockSizeSlider.ValueChanged += (_, _) => Save(c => c.DockIconSize = DockSizeSlider.Value);
        MagnificationSlider.ValueChanged += (_, _) => Save(c => c.DockMagnification = MagnificationSlider.Value);
        AutoHideCheck.Click += (_, _) => Save(c => c.DockAutoHide = AutoHideCheck.IsChecked == true);

        MenuBarEnabledCheck.Click += (_, _) => Save(c => c.MenuBarEnabled = MenuBarEnabledCheck.IsChecked == true);
        ShowWifiCheck.Click += (_, _) => Save(c => c.ShowWifi = ShowWifiCheck.IsChecked == true);
        ShowBatteryCheck.Click += (_, _) => Save(c => c.ShowBattery = ShowBatteryCheck.IsChecked == true);
        ShowVolumeCheck.Click += (_, _) => Save(c => c.ShowVolume = ShowVolumeCheck.IsChecked == true);
        ShowClockCheck.Click += (_, _) => Save(c => c.ShowClock = ShowClockCheck.IsChecked == true);

        AnimationQualityCombo.SelectionChanged += (_, _) => Save(c => c.AnimationQualityOverride =
            AnimationQualityCombo.SelectedIndex switch { 1 => "Full", 2 => "Reduced", 3 => "Minimal", _ => "Auto" });

        StartWithWindowsCheck.Click += (_, _) =>
        {
            Save(c => c.StartWithWindows = StartWithWindowsCheck.IsChecked == true);
            StartupManager.SetEnabled(StartWithWindowsCheck.IsChecked == true);
        };
    }

    private static string SelectedText(ComboBox combo) => (combo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

    private void Save(Action<AppSettings> mutate)
    {
        if (_loading) return;
        mutate(ConfigManager.Current);
        ConfigManager.SaveDebounced();
    }

    private void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        (System.Windows.Application.Current as App)?.RestoreOriginal();
    }
}
