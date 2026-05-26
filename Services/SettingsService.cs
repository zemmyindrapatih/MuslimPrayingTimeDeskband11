using System.IO;
using System.Text.Json;
using PrayingTime.Models;

namespace PrayingTime.Services;

public static class SettingsService
{
    private static readonly string SettingsPath =
        Path.Combine(AppContext.BaseDirectory, "settings.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to load settings from {SettingsPath}", ex);
        }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            string json = JsonSerializer.Serialize(settings, Options);
            File.WriteAllText(SettingsPath, json);
            Logger.Info($"Settings saved to {SettingsPath}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to save settings to {SettingsPath}", ex);
        }
    }
}
