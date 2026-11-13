using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace FloatingClock;

public static class WeatherService
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(15) };

    public static async Task<List<City>> SearchAsync(string query)
    {
        using var response = await Client.GetAsync($"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(query)}&count=10&language=zh&format=json");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var cities = new List<City>();
        if (json.RootElement.TryGetProperty("results", out var results))
        {
            foreach (var item in results.EnumerateArray())
            {
                var region = string.Join(" / ", new[] { "admin1", "country" }
                    .Select(key => item.TryGetProperty(key, out var value) ? value.GetString() : null)
                    .Where(value => !string.IsNullOrWhiteSpace(value)).Distinct());
                cities.Add(new City(item.GetProperty("name").GetString()!, region,
                    item.GetProperty("latitude").GetDouble(), item.GetProperty("longitude").GetDouble()));
            }
        }
        return cities;
    }

    public static async Task<(double Temperature, string Description)> GetAsync(City city)
    {
        var url = FormattableString.Invariant($"https://api.open-meteo.com/v1/forecast?latitude={city.Latitude}&longitude={city.Longitude}&current=temperature_2m,weather_code");
        using var response = await Client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var current = json.RootElement.GetProperty("current");
        return (current.GetProperty("temperature_2m").GetDouble(), Describe(current.GetProperty("weather_code").GetInt32()));
    }

    public static string Describe(int code) => code switch
    {
        0 => "晴", 1 => "大部晴朗", 2 => "多云", 3 => "阴",
        45 or 48 => "雾", 51 or 53 or 55 => "毛毛雨", 56 or 57 => "冻毛毛雨",
        61 or 63 or 65 => "雨", 66 or 67 => "冻雨", 71 or 73 or 75 or 77 => "雪",
        80 or 81 or 82 => "阵雨", 85 or 86 => "阵雪", 95 or 96 or 99 => "雷暴",
        _ => "天气未知"
    };
}
