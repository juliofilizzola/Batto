using System.Collections.Concurrent;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Batto.Models;

namespace Batto.Bluetooth;

public class BluetoothManager : IDisposable
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
        var type = ClassifyDeviceType(name, properties);

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

    public static DeviceType ClassifyDeviceType(string name, IReadOnlyDictionary<string, object?>? properties = null)
    {
        string lower = name.ToLowerInvariant();

        // 1. Audio Headsets & Headphones
        if (lower.Contains("headphone") || lower.Contains("headset") || lower.Contains("fone") ||
            lower.Contains("earbuds") || lower.Contains("buds") || lower.Contains("airpods") ||
            lower.Contains("freebuds") || lower.Contains("wh-1000") || lower.Contains("wf-1000") ||
            lower.Contains("galaxy buds") || lower.Contains("tws") || lower.Contains("tune") ||
            lower.Contains("live 660") || lower.Contains("live pro") || lower.Contains("jbl"))
        {
            if (lower.Contains("speaker") || lower.Contains("caixa") || lower.Contains("echo dot") || lower.Contains("flip") || lower.Contains("charge") || lower.Contains("boombox"))
                return DeviceType.Speaker;

            return DeviceType.Headphones;
        }

        // 2. Speakers
        if (lower.Contains("speaker") || lower.Contains("soundbar") || lower.Contains("caixa de som") ||
            lower.Contains("echo") || lower.Contains("alexa") || lower.Contains("nest") ||
            lower.Contains("boombox") || lower.Contains("charge") || lower.Contains("flip") ||
            lower.Contains("go 3") || lower.Contains("clip"))
        {
            return DeviceType.Speaker;
        }

        // 3. Mice
        if (lower.Contains("mouse") || lower.Contains("ratón") || lower.Contains("trackball") ||
            lower.Contains("mx master") || lower.Contains("mx anywhere") || lower.Contains("g305") ||
            lower.Contains("g502") || lower.Contains("viper") || lower.Contains("deathadder") ||
            lower.Contains("x11mouse"))
        {
            return DeviceType.Mouse;
        }

        // 4. Keyboards
        if (lower.Contains("keyboard") || lower.Contains("teclado") || lower.Contains("clavier") ||
            lower.Contains("keychron") || lower.Contains("ak820") || lower.Contains("mx keys") ||
            lower.Contains("k380") || lower.Contains("k810") || lower.Contains("anne pro"))
        {
            return DeviceType.Keyboard;
        }

        // 5. Gamepads & Controllers
        if (lower.Contains("controller") || lower.Contains("gamepad") || lower.Contains("joystick") ||
            lower.Contains("xbox") || lower.Contains("dualshock") || lower.Contains("dualsense") ||
            lower.Contains("pro controller") || lower.Contains("joy-con") || lower.Contains("switch"))
        {
            return DeviceType.Gamepad;
        }

        // 6. Stylus / Pen
        if (lower.Contains("pen") || lower.Contains("stylus") || lower.Contains("caneta") ||
            lower.Contains("pencil") || lower.Contains("surface pen"))
        {
            return DeviceType.Pen;
        }

        // 7. Phone / Tablet
        if (lower.Contains("iphone") || lower.Contains("galaxy") || lower.Contains("pixel") ||
            lower.Contains("phone") || lower.Contains("celular") || lower.Contains("ipad"))
        {
            return DeviceType.Phone;
        }

        return DeviceType.Generic;
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
