using Batto.Battery;

namespace Batto.Bluetooth;

/// <summary>
/// Windows battery source. Tries, in order: the PnP battery property of the device,
/// the battery of a service node with the same address, and the GATT Battery Service (0x180F).
/// </summary>
public sealed class WindowsBatteryReader : IBatteryReader
{
    private const string PnpSource = "Windows PnP";
    private const string PnpServiceNodeSource = "Windows PnP (serviço do dispositivo)";
    private const string GattSource = "GATT (0x180F)";

    private readonly PnpBatteryReader _pnpReader;
    private readonly GattBatteryReader _gattReader;

    public WindowsBatteryReader(PnpBatteryReader pnpReader, GattBatteryReader gattReader)
    {
        _pnpReader = pnpReader;
        _gattReader = gattReader;
        _gattReader.BatteryLevelReceived += OnGattLevelReceived;
    }

    public event Action<string, BatteryReading>? BatteryPushed;

    public async Task<BatteryReading?> ReadAsync(string deviceId, TimeSpan timeout)
    {
        var pnpBattery = await _pnpReader.ReadBatteryAsync(deviceId);
        if (pnpBattery.HasValue)
        {
            return new BatteryReading(pnpBattery.Value, PnpSource);
        }

        // Classic and BLE devices: Windows keeps the battery on a node with the same address.
        // Preferred over GATT because some keyboards report a stale GATT value.
        var address = PnpBatteryReader.ExtractClassicAddress(deviceId) ?? PnpBatteryReader.ExtractLeAddress(deviceId);
        if (address != null)
        {
            var serviceBattery = await _pnpReader.ReadBatteryByAddressAsync(address);
            if (serviceBattery.HasValue)
            {
                return new BatteryReading(serviceBattery.Value, PnpServiceNodeSource);
            }
        }

        var gattBattery = await _gattReader.ReadBatteryAsync(deviceId, timeout);
        return gattBattery.HasValue ? new BatteryReading(gattBattery.Value, GattSource) : null;
    }

    public void Forget(string deviceId) => _gattReader.UnregisterDevice(deviceId);

    public void Dispose()
    {
        _gattReader.BatteryLevelReceived -= OnGattLevelReceived;
        _gattReader.Dispose();
    }

    private void OnGattLevelReceived(string deviceId, int level) =>
        BatteryPushed?.Invoke(deviceId, new BatteryReading(level, GattSource));
}
