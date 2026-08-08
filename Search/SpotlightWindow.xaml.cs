using MacBookStyleForWindows.Services;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace MacBookStyleForWindows.Search;

public partial class SpotlightWindow : Window
{
    private readonly DispatcherTimer _debounce;
    private int _selectedIndex = -1;

    public SpotlightWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => NativeMethods.MakeToolWindow(this);

        // 80ms debounce: smooths out fast typing so we don't rebuild the results list every keystroke.
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); RunSearch(); };
    }

    public void ShowCentered()
    {
        var area = SystemParameters.WorkArea;
        UpdateLayout();
        Left = area.Left + (area.Width - Width) / 2;
        Top = area.Top + area.Height * 0.22;

        SearchBox.Text = string.Empty;
        ResultsList.Items.Clear();
        Show();
        Activate();
        SearchBox.Focus();

        if (PerformanceService.MagnificationEnabled)
        {
            Opacity = 0;
            var fade = new DoubleAnimation(1, PerformanceService.ScaledDuration(TimeSpan.FromMilliseconds(120)));
            BeginAnimation(OpacityProperty, fade);
        }
    }

    public void HideAnimated() => Hide();

    private void Window_Deactivated(object sender, EventArgs e) => Hide();

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _debounce.Stop();
        _debounce.Start();
    }

    private void RunSearch()
    {
        ResultsList.Items.Clear();
        _selectedIndex = -1;
        var query = SearchBox.Text;
        if (string.IsNullOrWhiteSpace(query)) return;

        var results = AppLauncher.Search(query);
        foreach (var app in results)
        {
            var row = new Border
            {
                Padding = new Thickness(14, 10, 14, 10),
                CornerRadius = new CornerRadius(10),
                Cursor = Cursors.Hand,
                Tag = app
            };
            row.Child = new TextBlock
            {
                Text = app.Name,
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
                FontSize = 15
            };
            row.MouseEnter += (_, _) => row.Background = (Brush)FindResource("SurfaceHoverBrush");
            row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;
            row.MouseLeftButtonUp += (_, _) => { AppLauncher.Launch(app); Hide(); };
            ResultsList.Items.Add(row);
        }
    }

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Hide(); return; }

        if (e.Key == Key.Enter && ResultsList.Items.Count > 0)
        {
            var first = (Border)ResultsList.Items[0];
            if (first.Tag is Services.AppEntry app) AppLauncher.Launch(app);
            Hide();
        }
    }
}
