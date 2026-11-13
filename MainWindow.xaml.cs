using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace FloatingClock;

public partial class MainWindow : Window
{
    private readonly Settings settings = Settings.Load();
    private readonly DispatcherTimer clock_timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly DispatcherTimer weather_timer = new() { Interval = TimeSpan.FromMinutes(15) };
    private TimeZoneInfo time_zone = TimeZoneInfo.Local;
    private SettingsWindow? settings_window;
    private int weather_request;
    private readonly System.Windows.Forms.NotifyIcon tray_icon;
    private readonly System.Windows.Forms.ContextMenuStrip tray_menu;
    private readonly System.Drawing.Icon tray_image;

    public MainWindow()
    {
        InitializeComponent();
        using (var icon_stream = Application.GetResourceStream(new Uri("pack://application:,,,/assets/clock.ico"))!.Stream)
        using (var source_icon = new System.Drawing.Icon(icon_stream, 32, 32))
        {
            tray_image = (System.Drawing.Icon)source_icon.Clone();
        }
        tray_menu = new System.Windows.Forms.ContextMenuStrip();
        tray_menu.Items.Add("显示 / 隐藏时钟", null, (_, _) => ToggleClock());
        tray_menu.Items.Add("设置…", null, (_, _) => OpenSettings(this, new RoutedEventArgs()));
        tray_menu.Items.Add("刷新天气", null, async (_, _) => await UpdateWeatherAsync());
        tray_menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        tray_menu.Items.Add("退出", null, (_, _) => Close());
        tray_icon = new System.Windows.Forms.NotifyIcon
        {
            Icon = tray_image,
            Text = "浮光时钟 · 双击显示或隐藏，右键设置",
            ContextMenuStrip = tray_menu,
            Visible = true
        };
        tray_icon.DoubleClick += (_, _) => ToggleClock();
        Loaded += (_, _) =>
        {
            RestorePosition();
            ApplySettings();
            clock_timer.Start();
            weather_timer.Start();
        };
        clock_timer.Tick += (_, _) => UpdateClock();
        weather_timer.Tick += async (_, _) => await UpdateWeatherAsync();
        Closing += OnClosing;
        SizeChanged += (_, _) => KeepVisible();
        SystemParameters.StaticPropertyChanged += DisplayChanged;
    }

    private void DisplayChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.VirtualScreenWidth) || e.PropertyName == nameof(SystemParameters.VirtualScreenHeight))
            Dispatcher.BeginInvoke(() => KeepVisible());
    }

    private void RestorePosition()
    {
        Left = settings.Left ?? SystemParameters.WorkArea.Right - Width - 30;
        Top = settings.Top ?? SystemParameters.WorkArea.Top + 40;
        KeepVisible();
    }

    private void KeepVisible()
    {
        if (!IsLoaded) return;
        Left = Math.Clamp(Left, SystemParameters.VirtualScreenLeft, Math.Max(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - ActualWidth));
        Top = Math.Clamp(Top, SystemParameters.VirtualScreenTop, Math.Max(SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - ActualHeight));
    }

    public void ApplySettings()
    {
        try { time_zone = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            time_zone = TimeZoneInfo.Local;
            settings.TimeZoneId = time_zone.Id;
        }
        Opacity = settings.Opacity;
        ApplyScale();
        UpdateClock();
        _ = UpdateWeatherAsync();
        SaveSettings();
    }

    private void UpdateClock()
    {
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, time_zone);
        ClockText.Text = now.ToString(settings.ShowSeconds ? "HH:mm:ss" : "HH:mm", CultureInfo.InvariantCulture);
        DateText.Text = now.ToString("yyyy年M月d日  dddd", CultureInfo.GetCultureInfo("zh-CN"));
        var offset = now.Offset;
        ZoneText.Text = $"{time_zone.StandardName}  ·  UTC{(offset < TimeSpan.Zero ? "−" : "+")}{offset.Duration():hh\\:mm}";
        ZoneText.ToolTip = time_zone.DisplayName;
    }

    public void ApplyScale()
    {
        Width = 340 * settings.Scale;
        ClockScale.ScaleX = settings.Scale;
        ClockScale.ScaleY = settings.Scale;
        UpdateLayout();
        KeepVisible();
        SaveSettings();
    }

    private async Task UpdateWeatherAsync()
    {
        var request = ++weather_request;
        WeatherPanel.Visibility = settings.ShowWeather ? Visibility.Visible : Visibility.Collapsed;
        if (!settings.ShowWeather) return;
        var city = settings.City;
        if (city is null)
        {
            WeatherText.Text = "请在设置中选择天气城市";
            WeatherDetail.Text = "天气城市与时区可以分别设置";
            return;
        }
        WeatherText.Text = $"{city.Name} · 正在获取天气…";
        WeatherDetail.Text = "Open-Meteo · 每15分钟更新";
        try
        {
            var weather = await WeatherService.GetAsync(city);
            if (request != weather_request) return;
            WeatherText.Text = $"{city.Name}   {weather.Temperature:0.#}°C  ·  {weather.Description}";
            WeatherDetail.Text = $"Open-Meteo · 更新于 {TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, time_zone):HH:mm}";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            if (request != weather_request) return;
            WeatherText.Text = $"{city.Name} · 天气暂不可用";
            WeatherDetail.Text = "请检查网络；15分钟后重试，或右键刷新";
        }
    }

    private void DragWindow(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is System.Windows.Controls.TextBlock || e.OriginalSource is System.Windows.Shapes.Ellipse || ReferenceEquals(e.OriginalSource, sender))
        {
            DragMove();
            KeepVisible();
            SaveSettings();
        }
    }

    private void OpenSettings(object sender, RoutedEventArgs e)
    {
        Show();
        if (settings_window is not null) { settings_window.Show(); settings_window.Activate(); return; }
        settings_window = new SettingsWindow(settings, this) { Owner = this };
        settings_window.Closed += (_, _) => settings_window = null;
        settings_window.Show();
    }

    private void ToggleClock()
    {
        if (IsVisible)
        {
            settings_window?.Hide();
            Hide();
        }
        else
        {
            Show();
            KeepVisible();
            Activate();
        }
    }

    private async void RefreshWeather(object sender, RoutedEventArgs e) => await UpdateWeatherAsync();
    private void ExitApp(object sender, RoutedEventArgs e) => Close();

    public void SaveSettings()
    {
        settings.Left = Left;
        settings.Top = Top;
        try { settings.Save(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ToolTip = "设置保存失败：" + ex.Message;
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        tray_icon.Visible = false;
        tray_icon.Dispose();
        tray_image.Dispose();
        tray_menu.Dispose();
        clock_timer.Stop();
        weather_timer.Stop();
        weather_request++;
        SystemParameters.StaticPropertyChanged -= DisplayChanged;
        SaveSettings();
    }
}
