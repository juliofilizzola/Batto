using System.IO;
using System.Text.Json;
using Batto.Models;
using Microsoft.Win32;

namespace Batto.Storage;

public class SettingsManager
{
    private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppRegistryName = "BattoBluetoothBatteryTray";

    private readonly string _settingsFolder;
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public AppSettings Settings { get; private set; }

    public SettingsManager(string? customFolder = null)
    {
        _settingsFolder = customFolder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Batto");
        _settingsPath = Path.Combine(_settingsFolder, "settings.json");

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

            // Sync Windows Registry startup
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
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
            if (key == null) return;

            string exePath = Environment.ProcessPath ?? string.Empty;
            if (string.IsNullOrEmpty(exePath)) return;

            if (Settings.StartWithWindows)
            {
                key.SetValue(AppRegistryName, $"\"{exePath}\" --minimized");
            }
            else
            {
                key.DeleteValue(AppRegistryName, false);
            }
        }
        catch
        {
            // Silently handle registry permissions
        }
    }

    public bool IsStartupEnabledInRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
            return key?.GetValue(AppRegistryName) != null;
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

            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
            key?.DeleteValue(AppRegistryName, false);
        }
        catch
        {
        }
    }
}
