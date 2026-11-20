using System.Collections.Concurrent;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;
using Batto.Models;

namespace Batto.Bluetooth;

public class GattBatteryReader : IDisposable
{
    private static readonly Guid BatteryServiceUuid = new("0000180F-0000-1000-8000-00805F9B34FB");
    private static readonly Guid BatteryLevelCharUuid = new("00002A19-0000-1000-8000-00805F9B34FB");

    private readonly ConcurrentDictionary<string, BluetoothLEDevice> _activeDevices = new();
    private readonly ConcurrentDictionary<string, GattCharacteristic> _subscribedCharacteristics = new();

    public event Action<string, int>? BatteryLevelReceived;

    public async Task<int?> ReadBatteryAsync(string deviceId, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            var bleDevice = await GetOrConnectDeviceAsync(deviceId, cts.Token);
            if (bleDevice == null) return null;

            var servicesResult = await bleDevice.GetGattServicesForUuidAsync(BatteryServiceUuid, BluetoothCacheMode.Uncached).AsTask(cts.Token);
            if (servicesResult.Status != GattCommunicationStatus.Success || servicesResult.Services.Count == 0)
            {
                return null;
            }

            foreach (var service in servicesResult.Services)
            {
                using (service)
                {
                    var charsResult = await service.GetCharacteristicsForUuidAsync(BatteryLevelCharUuid, BluetoothCacheMode.Uncached).AsTask(cts.Token);
                    if (charsResult.Status != GattCommunicationStatus.Success || charsResult.Characteristics.Count == 0)
                        continue;

                    var characteristic = charsResult.Characteristics[0];

                    // Subscribe to real-time notifications if not already subscribed
                    if (!_subscribedCharacteristics.ContainsKey(deviceId))
                    {
                        await TrySubscribeNotificationsAsync(deviceId, characteristic);
                    }

                    // Read current value
                    var readResult = await characteristic.ReadValueAsync(BluetoothCacheMode.Uncached).AsTask(cts.Token);
                    if (readResult.Status == GattCommunicationStatus.Success)
                    {
                        var reader = DataReader.FromBuffer(readResult.Value);
                        byte level = reader.ReadByte();
                        return Math.Clamp((int)level, 0, 100);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout reached - device probably in deep sleep
        }
        catch
        {
            // Device communication error
        }

        return null;
    }

    /// <summary>
    /// Reuses the open BLE device for this id. Opening a new one on every poll left the previous object alive
    /// (holding its connection) until the GC ran. A new one is opened only when the old one is no longer connected.
    /// </summary>
    private async Task<BluetoothLEDevice?> GetOrConnectDeviceAsync(string deviceId, CancellationToken token)
    {
        if (_activeDevices.TryGetValue(deviceId, out var existing) &&
            existing.ConnectionStatus == BluetoothConnectionStatus.Connected)
        {
            return existing;
        }

        // Stale or missing: release the old device and its notification subscription before opening a new one,
        // so the subscription is set up again on the new device.
        UnregisterDevice(deviceId);

        var device = await BluetoothLEDevice.FromIdAsync(deviceId).AsTask(token);
        if (device != null)
        {
            _activeDevices[deviceId] = device;
        }
        return device;
    }

    private async Task TrySubscribeNotificationsAsync(string deviceId, GattCharacteristic characteristic)
    {
        try
        {
            if (characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify) ||
                characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate))
            {
                var status = await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.Notify);

                if (status == GattCommunicationStatus.Success)
                {
                    characteristic.ValueChanged += (sender, args) =>
                    {
                        try
                        {
                            var reader = DataReader.FromBuffer(args.CharacteristicValue);
                            byte level = reader.ReadByte();
                            BatteryLevelReceived?.Invoke(deviceId, Math.Clamp((int)level, 0, 100));
                        }
                        catch
                        {
                        }
                    };
                    _subscribedCharacteristics[deviceId] = characteristic;
                }
            }
        }
        catch
        {
        }
    }

    public void UnregisterDevice(string deviceId)
    {
        if (_subscribedCharacteristics.TryRemove(deviceId, out var characteristic))
        {
            try
            {
                characteristic.Service?.Dispose();
            }
            catch
            {
            }
        }

        if (_activeDevices.TryRemove(deviceId, out var bleDevice))
        {
            try
            {
                bleDevice.Dispose();
            }
            catch
            {
            }
        }
    }

    public void Dispose()
    {
        foreach (var key in _subscribedCharacteristics.Keys.ToList())
        {
            UnregisterDevice(key);
        }
    }
}
