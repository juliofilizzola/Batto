namespace Batto.Linux.Bluez;

/// <summary>Immutable view of one BlueZ device, built from its Device1 and Battery1 properties.</summary>
public sealed record BlueZDeviceSnapshot(
    string Path,
    string Address,
    string Name,
    string? Icon,
    bool Paired,
    bool Connected,
    int? BatteryLevel);
