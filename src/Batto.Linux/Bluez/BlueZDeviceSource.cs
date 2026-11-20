using Batto.Bluetooth;
using Batto.Models;

namespace Batto.Linux.Bluez;

/// <summary>
/// Paired BlueZ devices as the core's device model. Only paired devices are exposed, the same rule the Windows app follows.
/// Models are created and changed on the thread that raises the registry events (the UI thread in the app).
/// </summary>
public sealed class BlueZDeviceSource : IBluetoothDeviceSource
{
    private readonly BlueZRegistry _registry;
    private readonly Dictionary<string, BluetoothDeviceModel> _models = new();
    private bool _started;

    public event Action<BluetoothDeviceModel>? DeviceDiscovered;
    public event Action<BluetoothDeviceModel>? DeviceUpdated;
    public event Action<string>? DeviceRemoved;

    public BlueZDeviceSource(BlueZRegistry registry)
    {
        _registry = registry;
        _registry.DeviceChanged += OnDeviceChanged;
        _registry.DeviceRemoved += OnDeviceRemoved;
    }

    public void StartDiscovery()
    {
        if (_started) return;
        _started = true;
        _ = StartRegistryAsync();
    }

    private async Task StartRegistryAsync()
    {
        try
        {
            await _registry.StartAsync();
        }
        catch (Exception ex)
        {
            CrashLogger.Log("BlueZ registry start", ex);
        }
    }

    private void OnDeviceChanged(BlueZDeviceSnapshot snapshot)
    {
        if (!snapshot.Paired)
        {
            OnDeviceRemoved(snapshot.Path);
            return;
        }

        var type = BlueZIconMapper.Map(snapshot.Icon) ?? DeviceClassifier.ClassifyDeviceType(snapshot.Name);

        if (!_models.TryGetValue(snapshot.Path, out var model))
        {
            model = new BluetoothDeviceModel
            {
                Id = BlueZIds.DeviceIdFor(snapshot.Address),
                Name = snapshot.Name,
                Type = type,
                IsConnected = snapshot.Connected,
                BluetoothAddress = BlueZIds.ToNumericAddress(snapshot.Address)
            };
            _models[snapshot.Path] = model;
            DeviceDiscovered?.Invoke(model);
            return;
        }

        // Battery changes travel through IBatteryReader; only identity, type and connection need a device update.
        bool changed = model.Name != snapshot.Name || model.IsConnected != snapshot.Connected || model.Type != type;
        if (!changed) return;

        model.Name = snapshot.Name;
        model.IsConnected = snapshot.Connected;
        model.Type = type;
        DeviceUpdated?.Invoke(model);
    }

    private void OnDeviceRemoved(string path)
    {
        if (_models.Remove(path, out var model))
        {
            DeviceRemoved?.Invoke(model.Id);
        }
    }

    public void Dispose()
    {
        _registry.DeviceChanged -= OnDeviceChanged;
        _registry.DeviceRemoved -= OnDeviceRemoved;
    }
}
