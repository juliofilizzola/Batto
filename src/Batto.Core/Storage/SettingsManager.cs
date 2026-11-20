using System.IO;
using System.Text.Json;
using Batto.Models;

namespace Batto.Storage;

public class SettingsManager
{
    private readonly string _settingsFolder;
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IAutostart? _autostart;

    public AppSettings Settings { get; private set; }

    public SettingsManager(string? customFolder = null, IAutostart? autostart = null)
    {
        _settingsFolder = customFolder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Batto");
        _settingsPath = Path.Combine(_settingsFolder, "settings.json");
        _autostart = autostart;

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        Settings = Load();
    }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                string json = File.ReadAllText(_settingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
                if (loaded != null)
                {
                    Settings = loaded;
                    return loaded;
                }
            }
        }
        catch
        {
            // Fallback to default
        }

        Settings = new AppSettings();
        return Settings;
    }

    public void Save()
    {
        try
        {
            if (!Directory.Exists(_settingsFolder))
            {
                Directory.CreateDirectory(_settingsFolder);
            }

            string json = JsonSerializer.Serialize(Settings, _jsonOptions);
            File.WriteAllText(_settingsPath, json);

            ApplyStartupSettings();
        }
        catch
        {
            // Silently handle IO errors in background
        }
    }

    public void ApplyStartupSettings()
    {
        try
        {
            string exePath = Environment.ProcessPath ?? string.Empty;
            if (string.IsNullOrEmpty(exePath) || _autostart == null) return;

            if (Settings.StartWithWindows)
            {
                _autostart.Enable(exePath);
            }
            else
            {
                _autostart.Disable();
            }
        }
        catch
        {
            // Silently handle platform permission errors
        }
    }

    public bool IsStartupEnabled()
    {
        try
        {
            return _autostart?.IsEnabled() ?? false;
        }
        catch
        {
            return false;
        }
    }

    public void ClearAllSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                File.Delete(_settingsPath);
            }

            _autostart?.Disable();
        }
        catch
        {
        }
    }
}
