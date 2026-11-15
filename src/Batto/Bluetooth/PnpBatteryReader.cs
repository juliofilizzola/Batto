using System.Text.RegularExpressions;
using Windows.Devices.Enumeration;

namespace Batto.Bluetooth;

public class PnpBatteryReader
{
    public const string PnpBatteryLevelKey = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";
    public const string PnpIsConnectedKey = "{83DA6326-97A6-4088-9453-A1923F573B29} 15";
    public const string AepIsConnectedKey = "System.Devices.Aep.IsConnected";

    public static readonly string[] RequiredProperties = new[]
    {
        PnpBatteryLevelKey,
        PnpIsConnectedKey,
        AepIsConnectedKey,
        "System.ItemNameDisplay",
        "System.Devices.Aep.DeviceAddress"
    };

    public static int? ExtractBatteryLevel(IReadOnlyDictionary<string, object?> properties)
    {
        if (properties.TryGetValue(PnpBatteryLevelKey, out var value) && value != null)
        {
            return ParseBatteryValue(value);
        }

        return null;
    }

    public static bool? ExtractIsConnected(IReadOnlyDictionary<string, object?> properties)
    {
        if (properties.TryGetValue(AepIsConnectedKey, out var aepVal) && aepVal is bool aepBool)
        {
            return aepBool;
        }

        if (properties.TryGetValue(PnpIsConnectedKey, out var pnpVal))
        {
            if (pnpVal is bool pnpBool) return pnpBool;
            if (pnpVal is int pnpInt) return pnpInt != 0;
            if (pnpVal is string pnpStr && bool.TryParse(pnpStr, out var parsed)) return parsed;
        }

        return null;
    }

    public static int? ParseBatteryValue(object value)
    {
        try
        {
            return value switch
            {
                byte b => Math.Clamp((int)b, 0, 100),
                sbyte sb => Math.Clamp((int)sb, 0, 100),
                short s => Math.Clamp((int)s, 0, 100),
                ushort us => Math.Clamp((int)us, 0, 100),
                int i => Math.Clamp(i, 0, 100),
                uint ui => Math.Clamp((int)ui, 0, 100),
                long l => Math.Clamp((int)l, 0, 100),
                ulong ul => Math.Clamp((int)ul, 0, 100),
                double d => Math.Clamp((int)Math.Round(d), 0, 100),
                float f => Math.Clamp((int)Math.Round(f), 0, 100),
                string str when int.TryParse(str.Replace("%", "").Trim(), out var parsed) => Math.Clamp(parsed, 0, 100),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task<int?> ReadBatteryAsync(string deviceId)
    {
        try
        {
            var deviceInfo = await DeviceInformation.CreateFromIdAsync(deviceId, RequiredProperties);
            if (deviceInfo != null)
            {
                return ExtractBatteryLevel(deviceInfo.Properties);
            }
        }
        catch
        {
        }

        return null;
    }

    private static readonly Regex ClassicDeviceIdAddress = new(@"Bluetooth#Bluetooth[0-9a-fA-F:]{17}-([0-9a-fA-F]{2}(?::[0-9a-fA-F]{2}){5})$", RegexOptions.Compiled);
    private static readonly Regex ServiceNodeAddress = new(@"(?:&0&|DEV_)([0-9A-Fa-f]{12})(?:_|$)", RegexOptions.Compiled);

    /// <summary>
    /// Returns the 12-digit upper-case address (e.g. C433960BEFD0) for a classic Bluetooth device id,
    /// or null for BLE and other ids.
    /// </summary>
    public static string? ExtractClassicAddress(string deviceId)
    {
        var match = ClassicDeviceIdAddress.Match(deviceId);
        return match.Success ? match.Groups[1].Value.Replace(":", "").ToUpperInvariant() : null;
    }

    /// <summary>
    /// Classic headsets often expose battery on a service node (for example the Hands-Free
    /// service) instead of on the device node. Scans every node that belongs to the same address.
    /// </summary>
    public async Task<int?> ReadBatteryByAddressAsync(string address)
    {
        try
        {
            var nodes = await DeviceInformation.FindAllAsync(string.Empty, RequiredProperties, DeviceInformationKind.Device);
            foreach (var node in nodes)
            {
                if (!node.Id.StartsWith(@"BTHENUM\", StringComparison.OrdinalIgnoreCase)) continue;

                var match = ServiceNodeAddress.Match(node.Id);
                if (!match.Success || !string.Equals(match.Groups[1].Value, address, StringComparison.OrdinalIgnoreCase)) continue;

                var level = ExtractBatteryLevel(node.Properties);
                if (level.HasValue) return level;
            }
        }
        catch
        {
        }

        return null;
    }
}
