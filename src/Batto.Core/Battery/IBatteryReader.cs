namespace Batto.Battery;

/// <summary>A battery level read from a device, with a label describing where it came from.</summary>
public sealed record BatteryReading(int Level, string Source);

/// <summary>
/// Platform battery source. The Windows app implements it with WinRT (PnP, GATT);
/// a Linux port implements it with BlueZ over D-Bus.
/// </summary>
public interface IBatteryReader : IDisposable
{
    /// <summary>Raised when the platform pushes a new level without being asked (for example a GATT notification).</summary>
    event Action<string, BatteryReading>? BatteryPushed;

    /// <summary>Reads the battery of one device, or returns null when the device exposes no level.</summary>
    Task<BatteryReading?> ReadAsync(string deviceId, TimeSpan timeout);

    /// <summary>Drops any subscription or cached connection held for a device that was removed.</summary>
    void Forget(string deviceId);
}
