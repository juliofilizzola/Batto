using Batto.Battery;

namespace Batto.Linux.Bluez;

/// <summary>
/// Battery from BlueZ's Battery1.Percentage. BlueZ fills that interface for the GATT Battery Service (BLE mice and keyboards)
/// and for the Hands-Free profile (headsets), so one source covers both cases.
/// Pushes are raised when the level changes. Both methods run on the UI thread, except ReadAsync, which only reads the registry.
/// </summary>
public sealed class BlueZBatteryReader : IBatteryReader
{
    private readonly BlueZRegistry _registry;
    private readonly Dictionary<string, int> _lastPushedLevels = new();

    public event Action<string, BatteryReading>? BatteryPushed;

    public BlueZBatteryReader(BlueZRegistry registry)
    {
        _registry = registry;
        _registry.DeviceChanged += OnDeviceChanged;
    }

    public Task<BatteryReading?> ReadAsync(string deviceId, TimeSpan timeout)
    {
        var address = BlueZIds.AddressFromId(deviceId);
        if (address != null
            && _registry.TryGetBatteryLevel(address, out var level)
            && level is int value)
        {
            return Task.FromResult<BatteryReading?>(new BatteryReading(value, BlueZNames.BatterySourceLabel));
        }

        return Task.FromResult<BatteryReading?>(null);
    }

    public void Forget(string deviceId)
    {
        var address = BlueZIds.AddressFromId(deviceId);
        if (address != null) _lastPushedLevels.Remove(address);
    }

    private void OnDeviceChanged(BlueZDeviceSnapshot snapshot)
    {
        if (!snapshot.Paired || snapshot.BatteryLevel is not int level) return;

        if (_lastPushedLevels.TryGetValue(snapshot.Address, out var last) && last == level) return;

        _lastPushedLevels[snapshot.Address] = level;
        BatteryPushed?.Invoke(
            BlueZIds.DeviceIdFor(snapshot.Address),
            new BatteryReading(level, BlueZNames.BatterySourceLabel));
    }

    public void Dispose() => _registry.DeviceChanged -= OnDeviceChanged;
}
