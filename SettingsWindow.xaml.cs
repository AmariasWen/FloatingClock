using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;

namespace FloatingClock;

public partial class SettingsWindow : Window
{
    private readonly Settings settings;
    private readonly MainWindow clock;
    private bool ready;

    public SettingsWindow(Settings settings, MainWindow clock)
    {
        InitializeComponent();
        this.settings = settings;
        this.clock = clock;
        Icon = clock.Icon;
        ZonePicker.ItemsSource = TimeZoneInfo.GetSystemTimeZones();
        ZonePicker.SelectedValue = settings.TimeZoneId;
        SecondsCheck.IsChecked = settings.ShowSeconds;
        WeatherCheck.IsChecked = settings.ShowWeather;
        OpacitySlider.Value = settings.Opacity * 100;
        OpacityLabel.Text = $"{OpacitySlider.Value:0}%";
        ScaleSlider.Value = settings.Scale * 100;
        ScaleLabel.Text = $"{ScaleSlider.Value:0}%";
        if (settings.City is not null)
        {
            CityQuery.Text = settings.City.Name;
            CityPicker.ItemsSource = new[] { settings.City };
            CityPicker.SelectedIndex = 0;
        }
        ready = true;
    }

    private void ZoneChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ready || ZonePicker.SelectedValue is not string zone_id) return;
        settings.TimeZoneId = zone_id;
        clock.ApplySettings();
    }

    private void OptionsChanged(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        settings.ShowSeconds = SecondsCheck.IsChecked == true;
        settings.ShowWeather = WeatherCheck.IsChecked == true;
        clock.ApplySettings();
    }

    private void OpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!ready) return;
        settings.Opacity = OpacitySlider.Value / 100;
        OpacityLabel.Text = $"{OpacitySlider.Value:0}%";
        clock.Opacity = settings.Opacity;
        clock.SaveSettings();
    }

    private void ScaleChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!ready) return;
        settings.Scale = ScaleSlider.Value / 100;
        ScaleLabel.Text = $"{ScaleSlider.Value:0}%";
        clock.ApplyScale();
    }

    private async void SearchCity(object sender, RoutedEventArgs e)
    {
        var query = CityQuery.Text.Trim();
        if (query.Length < 2) { SearchStatus.Text = "请至少输入两个字符，例如 北京 或 London。"; return; }
        SearchButton.IsEnabled = false;
        SearchStatus.Text = "正在搜索城市…";
        try
        {
            var cities = await WeatherService.SearchAsync(query);
            CityPicker.ItemsSource = cities;
            SearchStatus.Text = cities.Count == 0 ? "没有找到城市，请尝试英文名或添加国家，例如 Paris, France。" : "请选择正确的城市，避免同名城市混淆。";
            CityPicker.IsDropDownOpen = cities.Count != 0;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            SearchStatus.Text = "城市搜索失败，请检查网络后重试。";
        }
        finally { SearchButton.IsEnabled = true; }
    }

    private void SearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && SearchButton.IsEnabled) SearchCity(sender, e);
    }

    private void CityChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ready || CityPicker.SelectedItem is not City city) return;
        settings.City = city;
        settings.ShowWeather = true;
        WeatherCheck.IsChecked = true;
        clock.ApplySettings();
    }

    private void OpenWeatherSite(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void Done(object sender, RoutedEventArgs e) => Close();
}
