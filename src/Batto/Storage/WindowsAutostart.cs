using Microsoft.Win32;

namespace Batto.Storage;

/// <summary>Autostart through the per-user Run registry key.</summary>
public sealed class WindowsAutostart : IAutostart
{
    private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppRegistryName = "BattoBluetoothBatteryTray";
    private const string StartMinimizedArgument = "--minimized";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
        return key?.GetValue(AppRegistryName) != null;
    }

    public void Enable(string executablePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
        key?.SetValue(AppRegistryName, $"\"{executablePath}\" {StartMinimizedArgument}");
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
        key?.DeleteValue(AppRegistryName, false);
    }
}
