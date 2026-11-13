using System.IO;
using System.Text.Json;

namespace FloatingClock;

public sealed class Settings
{
    public string TimeZoneId { get; set; } = TimeZoneInfo.Local.Id;
    public double Opacity { get; set; } = 0.95;
    public double Scale { get; set; } = 1;
    public double? Left { get; set; }
    public double? Top { get; set; }
    public bool ShowSeconds { get; set; } = true;
    public bool ShowWeather { get; set; }
    public City? City { get; set; }

    public static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FloatingClock", "settings.json");

    public static Settings Load()
    {
        try
        {
            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
            settings.Opacity = Math.Clamp(settings.Opacity, 0.25, 1);
            settings.Scale = Math.Clamp(settings.Scale, 0.6, 1.8);
            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new Settings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temp_path = FilePath + ".tmp";
        File.WriteAllText(temp_path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp_path, FilePath, true);
    }
}

public sealed record City(string Name, string Region, double Latitude, double Longitude)
{
    public override string ToString() => $"{Name} · {Region}";
}
