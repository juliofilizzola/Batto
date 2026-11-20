using Batto.Linux.UI;
using Batto.Models;

namespace Batto.Linux;

/// <summary>Routes the UI's requests to the services, and the window and shutdown requests to the app shell.</summary>
public sealed class ShellActions : IDeviceActions
{
    private readonly AppServices _services;
    private readonly Action _hideWindow;
    private readonly Action _quit;

    public ShellActions(AppServices services, Action hideWindow, Action quit)
    {
        _services = services;
        _hideWindow = hideWindow;
        _quit = quit;
    }

    public void SetTrayVisibility(BluetoothDeviceModel device, bool isVisible) =>
        _services.Monitor.SetDeviceTrayVisibility(device, isVisible);

    public void SetPriority(BluetoothDeviceModel device, int priority) =>
        _services.Monitor.SetDevicePriority(device, priority);

    public void SetDeviceType(BluetoothDeviceModel device, DeviceType type) =>
        _services.Monitor.SetDeviceType(device, type);

    /// <summary>Saves the settings. The core applies the autostart entry as part of the save.</summary>
    public void SaveSettings() => _services.Settings.Save();

    public void RefreshNow() => _ = _services.Monitor.RefreshAllDevicesAsync();

    public void MinimizeToTray() => _hideWindow();

    public void Quit() => _quit();
}
