using System.Text.Json;
using WinClipboard.Core.Models;

namespace WinClipboard.Data;

/// <summary>Loads/saves <see cref="AppSettings"/> as JSON next to the SQLite database.</summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _settingsPath;

    public SettingsStore(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    /// <summary>Default location: %LOCALAPPDATA%\WinClipboard\settings.json</summary>
    public static string DefaultSettingsPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "WinClipboard", "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (JsonException)
        {
            // Corrupt settings file — fall back to defaults rather than crashing startup.
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }
}
