using Tmds.DBus.Protocol;

namespace Batto.Linux.Bluez;

/// <summary>Typed access to the D-Bus variants BlueZ sends. Returns null when the key is missing or has another type.</summary>
public static class BlueZValues
{
    public static string? String(IReadOnlyDictionary<string, VariantValue> properties, string key) =>
        properties.TryGetValue(key, out var value) && value.Type == VariantValueType.String ? value.GetString() : null;

    public static bool? Bool(IReadOnlyDictionary<string, VariantValue> properties, string key) =>
        properties.TryGetValue(key, out var value) && value.Type == VariantValueType.Bool ? value.GetBool() : null;

    public static int? Byte(IReadOnlyDictionary<string, VariantValue> properties, string key) =>
        properties.TryGetValue(key, out var value) && value.Type == VariantValueType.Byte ? (int)value.GetByte() : null;
}
