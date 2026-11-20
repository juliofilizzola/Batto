using System.Globalization;

namespace Batto.Linux.Bluez;

/// <summary>Maps BlueZ addresses to the device ids used by the core, and back.</summary>
public static class BlueZIds
{
    private const int AddressHexDigits = 12;
    private const int HexRadix = 16;

    public static string DeviceIdFor(string address) =>
        BlueZNames.DeviceIdPrefix + address.ToUpperInvariant();

    /// <summary>The BlueZ address (AA:BB:CC:DD:EE:FF) for a core device id, or null when the id is not a BlueZ one.</summary>
    public static string? AddressFromId(string deviceId)
    {
        if (!deviceId.StartsWith(BlueZNames.DeviceIdPrefix, StringComparison.Ordinal)) return null;
        return deviceId[BlueZNames.DeviceIdPrefix.Length..];
    }

    /// <summary>The address as a 48-bit number, as the core stores it in BluetoothAddress.</summary>
    public static ulong ToNumericAddress(string address)
    {
        string hex = address.Replace(":", string.Empty);
        return hex.Length == AddressHexDigits
            && ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)
                ? value
                : 0;
    }
}
