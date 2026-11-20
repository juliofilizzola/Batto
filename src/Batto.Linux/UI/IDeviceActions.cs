using Batto.Models;

namespace Batto.Linux.UI;

/// <summary>What the UI can ask the services to do. Implemented by the app; faked in tests.</summary>
public interface IDeviceActions
{
    void SetTrayVisibility(BluetoothDeviceModel device, bool isVisible);

    void SetPriority(BluetoothDeviceModel device, int priority);

    void SetDeviceType(BluetoothDeviceModel device, DeviceType type);

    void SaveSettings();

    void RefreshNow();

    void MinimizeToTray();

    void Quit();
}
