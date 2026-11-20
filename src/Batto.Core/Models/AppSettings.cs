using System.Text.Json.Serialization;

namespace Batto.Models;

public class DevicePreference
{
    public bool IsVisibleInTray { get; set; } = true;
    public int Priority { get; set; } = 0;
    public string? CustomDisplayName { get; set; }
    /// <summary>User-chosen icon type; null keeps the type detected from the device name.</summary>
    public DeviceType? TypeOverride { get; set; }
}

public class AppSettings
{
    public int RefreshIntervalSeconds { get; set; } = 60;
    public bool StartWithWindows { get; set; } = false;
    public bool StartMinimized { get; set; } = true;
    public bool NotifyLowBattery { get; set; } = true;
    public bool NotifyCriticalBattery { get; set; } = true;
    public bool NotifyConnected { get; set; } = true;
    public bool NotifyDisconnected { get; set; } = true;
    public int MaxTrayIcons { get; set; } = 6;

    /// <summary>Keep Batto icons promoted out of the Windows 11 overflow (^) menu.</summary>
    public bool PromoteTrayIcons { get; set; } = true;

    public Dictionary<string, DevicePreference> DevicePreferences { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public DevicePreference GetPreference(string deviceId)
    {
        if (DevicePreferences.TryGetValue(deviceId, out var pref))
            return pref;

        var newPref = new DevicePreference();
        DevicePreferences[deviceId] = newPref;
        return newPref;
    }
}
