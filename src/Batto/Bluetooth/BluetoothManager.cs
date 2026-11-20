using System.Collections.Concurrent;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Batto.Models;

namespace Batto.Bluetooth;

public class BluetoothManager : IBluetoothDeviceSource
{
    private readonly ConcurrentDictionary<string, BluetoothDeviceModel> _devices = new();
    private readonly List<DeviceWatcher> _watchers = new();
    private bool _isScanning;

    public event Action<BluetoothDeviceModel>? DeviceDiscovered;
    public event Action<BluetoothDeviceModel>? DeviceUpdated;
    public event Action<string>? DeviceRemoved;

    public IReadOnlyCollection<BluetoothDeviceModel> Devices => _devices.Values.ToList();

    public void StartDiscovery()
    {
        if (_isScanning) return;
        _isScanning = true;

        // BLE devices (GATT): association endpoints for the Bluetooth LE protocol
        StartWatcher(
            "(System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\") AND " +
            "System.Devices.Aep.IsPaired:=System.StructuredQueryType.Boolean#True",
            DeviceInformationKind.AssociationEndpoint);

        // Classic Bluetooth devices (headsets, speakers, many mice and keyboards)
        StartWatcher(
            BluetoothDevice.GetDeviceSelectorFromPairingState(true),
            DeviceInformationKind.Device);
    }

    private void StartWatcher(string selector, DeviceInformationKind kind)
    {
        var watcher = DeviceInformation.CreateWatcher(selector, PnpBatteryReader.RequiredProperties, kind);
        watcher.Added += OnDeviceAdded;
        watcher.Updated += OnDeviceUpdated;
        watcher.Removed += OnDeviceRemoved;
        _watchers.Add(watcher);
        watcher.Start();
    }

    private void OnDeviceAdded(DeviceWatcher sender, DeviceInformation deviceInfo)
    {
        var model = CreateOrUpdateModel(deviceInfo.Id, deviceInfo.Name, deviceInfo.Properties);
        _devices[deviceInfo.Id] = model;
        DeviceDiscovered?.Invoke(model);
    }

    private void OnDeviceUpdated(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        if (_devices.TryGetValue(update.Id, out var model))
        {
            bool hasChanged = false;

            // Check connection status
            var isConnected = PnpBatteryReader.ExtractIsConnected(update.Properties);
            if (isConnected.HasValue && model.IsConnected != isConnected.Value)
            {
                model.IsConnected = isConnected.Value;
                hasChanged = true;
            }

            // Check battery level from PnP
            var battery = PnpBatteryReader.ExtractBatteryLevel(update.Properties);
            if (battery.HasValue)
            {
                model.BatteryLevel = battery.Value;
                model.HasBatteryInfo = true;
                model.BatterySource = "Windows PnP";
                model.LastUpdated = DateTime.Now;
                hasChanged = true;
            }

            if (hasChanged)
            {
                DeviceUpdated?.Invoke(model);
            }
        }
    }

    private void OnDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        if (_devices.TryRemove(update.Id, out _))
        {
            DeviceRemoved?.Invoke(update.Id);
        }
    }

    public BluetoothDeviceModel CreateOrUpdateModel(string id, string name, IReadOnlyDictionary<string, object?> properties)
    {
        var isConnected = PnpBatteryReader.ExtractIsConnected(properties) ?? false;
        var batteryLevel = PnpBatteryReader.ExtractBatteryLevel(properties);
        var type = DeviceClassifier.ClassifyDeviceType(name, properties);

        var model = new BluetoothDeviceModel
        {
            Id = id,
            Name = string.IsNullOrWhiteSpace(name) ? "Dispositivo Bluetooth" : name,
            Type = type,
            IsConnected = isConnected,
            BatteryLevel = batteryLevel,
            HasBatteryInfo = batteryLevel.HasValue,
            BatterySource = batteryLevel.HasValue ? "Windows PnP" : string.Empty,
            LastUpdated = batteryLevel.HasValue ? DateTime.Now : null
        };

        return model;
    }

    public void StopDiscovery()
    {
        if (!_isScanning) return;
        _isScanning = false;

        foreach (var watcher in _watchers)
        {
            watcher.Stop();
        }
        _watchers.Clear();
    }

    public void Dispose()
    {
        StopDiscovery();
        _devices.Clear();
    }
}
