using MacBookStyleForWindows.Services;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace MacBookStyleForWindows.Dock;

public partial class DockWindow : Window
{
    private const double BaseIconSize = 52;
    private DispatcherTimer? _autoHideTimer;
    private bool _isRevealed = true;

    public DockWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        NativeMethods.MakeToolWindow(this);
        if (PerformanceService.BlurEnabled && ConfigManager.Current.TransparencyEnabled)
            NativeMethods.EnableBlur(this);

        BuildIcons();
        PositionWindow();

        IconStrip.MouseMove += OnIconStripMouseMove;
        IconStrip.MouseLeave += OnIconStripMouseLeave;

        if (ConfigManager.Current.DockAutoHide)
            EnableAutoHide();
    }

    /// <summary>Called once background hardware detection finishes (see App.OnStartup) — retroactively
    /// disables blur/magnification if the machine turned out to be low-end, without ever blocking startup.</summary>
    public void ApplyPerformanceProfile()
    {
        if (!PerformanceService.BlurEnabled)
            DockSurface.Effect = null; // drop the drop-shadow too on weak GPUs — cheap win
    }

    private void PositionWindow()
    {
        var area = SystemParameters.WorkArea;
        UpdateLayout();

        switch (ConfigManager.Current.DockPosition)
        {
            case "Left":
                Left = 4;
                Top = area.Top + (area.Height - ActualHeight) / 2;
                break;
            case "Right":
                Left = area.Right - ActualWidth - 4;
                Top = area.Top + (area.Height - ActualHeight) / 2;
                break;
            default: // Bottom
                Left = area.Left + (area.Width - ActualWidth) / 2;
                Top = area.Bottom - ActualHeight;
                break;
        }
    }

    // ---------------- Icons ----------------

    private void BuildIcons()
    {
        IconStrip.Children.Clear();
        var pinned = ConfigManager.Current.PinnedApps;

        // Sensible defaults on first run so the dock isn't empty.
        if (pinned.Count == 0)
        {
            pinned.AddRange(new[] { "File Explorer", "Microsoft Edge", "Settings" });
        }

        foreach (var appName in pinned)
        {
            var entry = AppLauncher.AllApps.FirstOrDefault(a =>
                a.Name.Equals(appName, StringComparison.OrdinalIgnoreCase));

            var icon = CreateIconButton(appName, entry);
            IconStrip.Children.Add(icon);
        }
    }

    private Button CreateIconButton(string name, Services.AppEntry? entry)
    {
        var container = new Grid { Width = BaseIconSize, Height = BaseIconSize, Margin = new Thickness(4, 0, 4, 0) };

        var button = new Button
        {
            Style = (Style)FindResource("DockIconButton"),
            Width = BaseIconSize,
            Height = BaseIconSize,
            ToolTip = name,
            RenderTransformOrigin = new Point(0.5, 1.0),
            RenderTransform = new ScaleTransform(1, 1)
        };

        button.Content = new TextBlock
        {
            Text = name.Length > 0 ? name[0].ToString().ToUpper() : "?",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        button.Click += (_, _) =>
        {
            if (entry != null) AppLauncher.Launch(entry);
        };

        button.MouseRightButtonUp += (_, e) =>
        {
            ShowIconContextMenu(name, button);
            e.Handled = true;
        };

        container.Children.Add(button);

        // Running indicator dot — checked once when the icon is created / dock shown,
        // not on a repeating timer.
        if (IsRunning(name))
        {
            var dot = new Ellipse
            {
                Width = 5, Height = 5,
                Fill = (Brush)FindResource("RunningIndicatorBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, -8)
            };
            container.Children.Add(dot);
        }

        return button;
    }

    private static bool IsRunning(string appName)
    {
        try
        {
            // Cheap heuristic match; good enough for a visual indicator, not security-critical.
            return Process.GetProcesses().Any(p =>
                p.ProcessName.Contains(appName.Split(' ')[0], StringComparison.OrdinalIgnoreCase));
        }
        catch { return false; }
    }

    private void ShowIconContextMenu(string appName, FrameworkElement target)
    {
        var menu = new ContextMenu();
        var removeItem = new MenuItem { Header = "Remove from Dock" };
        removeItem.Click += (_, _) =>
        {
            ConfigManager.Current.PinnedApps.Remove(appName);
            ConfigManager.SaveDebounced();
            BuildIcons();
        };
        menu.Items.Add(removeItem);

        var quitItem = new MenuItem { Header = "Quit" };
        quitItem.Click += (_, _) =>
        {
            try
            {
                var proc = Process.GetProcesses()
                    .FirstOrDefault(p => p.ProcessName.Contains(appName.Split(' ')[0], StringComparison.OrdinalIgnoreCase));
                proc?.CloseMainWindow();
            }
            catch { /* ignore */ }
        };
        menu.Items.Add(quitItem);

        target.ContextMenu = menu;
        menu.IsOpen = true;
    }

    // ---------------- Magnification (event-driven, zero idle CPU) ----------------

    private void OnIconStripMouseMove(object sender, MouseEventArgs e)
    {
        if (!PerformanceService.MagnificationEnabled) return;

        var mousePos = e.GetPosition(IconStrip);
        double maxScale = ConfigManager.Current.DockMagnification;

        foreach (var child in IconStrip.Children.OfType<Button>())
        {
            var center = child.TranslatePoint(new Point(child.ActualWidth / 2, child.ActualHeight / 2), IconStrip);
            double distance = Math.Abs(mousePos.X - center.X);

            // macOS-like falloff: full magnification within ~30px, tapering to 1.0 by ~120px.
            double influence = Math.Max(0, 1 - distance / 120.0);
            double scale = 1 + (maxScale - 1) * Math.Pow(influence, 2);

            AnimateScale(child, scale);
        }
    }

    private void OnIconStripMouseLeave(object sender, MouseEventArgs e)
    {
        foreach (var child in IconStrip.Children.OfType<Button>())
            AnimateScale(child, 1.0);
    }

    private void AnimateScale(Button button, double target)
    {
        if (button.RenderTransform is not ScaleTransform st) return;

        var duration = PerformanceService.ScaledDuration(TimeSpan.FromMilliseconds(120));
        if (duration == TimeSpan.Zero)
        {
            st.ScaleX = st.ScaleY = target;
            return;
        }

        var anim = new DoubleAnimation(target, new Duration(duration)) { EasingFunction = new QuadraticEase() };
        st.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
        st.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
    }

    // ---------------- Auto-hide (event-driven) ----------------

    private void EnableAutoHide()
    {
        MouseEnter += (_, _) => { _autoHideTimer?.Stop(); Reveal(); };
        MouseLeave += (_, _) =>
        {
            _autoHideTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            _autoHideTimer.Tick -= HideTick;
            _autoHideTimer.Tick += HideTick;
            _autoHideTimer.Start();
        };
        Hide_ToEdge();
    }

    private void HideTick(object? sender, EventArgs e)
    {
        _autoHideTimer!.Stop();
        Hide_ToEdge();
    }

    private void Reveal()
    {
        if (_isRevealed) return;
        _isRevealed = true;
        var area = SystemParameters.WorkArea;
        AnimateTop(area.Bottom - ActualHeight);
    }

    private void Hide_ToEdge()
    {
        if (!_isRevealed) return;
        _isRevealed = false;
        var area = SystemParameters.WorkArea;
        AnimateTop(area.Bottom - 4);
    }

    private void AnimateTop(double target)
    {
        var duration = PerformanceService.ScaledDuration(TimeSpan.FromMilliseconds(200));
        if (duration == TimeSpan.Zero) { Top = target; return; }
        var anim = new DoubleAnimation(target, new Duration(duration)) { EasingFunction = new QuadraticEase() };
        BeginAnimation(TopProperty, anim);
    }

    public void ToggleVisibility() => Visibility = Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
}
