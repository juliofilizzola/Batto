using Batto.Models;

namespace Batto.Bluetooth;

/// <summary>
/// Platform device discovery. The Windows app implements it with Windows.Devices (DeviceWatcher);
/// a Linux port implements it with BlueZ ObjectManager over D-Bus.
/// </summary>
public interface IBluetoothDeviceSource : IDisposable
{
    event Action<BluetoothDeviceModel>? DeviceDiscovered;
    event Action<BluetoothDeviceModel>? DeviceUpdated;
    event Action<string>? DeviceRemoved;

    void StartDiscovery();
}
