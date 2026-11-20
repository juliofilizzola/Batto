using Batto.Models;

namespace Batto.Linux.Bluez;

/// <summary>Maps the freedesktop icon name that BlueZ reports (Device1.Icon) to a device type.</summary>
public static class BlueZIconMapper
{
    /// <summary>The device type for the icon, or null when the icon does not say (the caller then falls back to the name).</summary>
    public static DeviceType? Map(string? icon) => icon switch
    {
        "audio-headset" => DeviceType.Headset,
        "audio-headphones" => DeviceType.Headphones,
        "audio-card" or "audio-speakers" => DeviceType.Speaker,
        "input-mouse" => DeviceType.Mouse,
        "input-keyboard" => DeviceType.Keyboard,
        "input-gaming" => DeviceType.Gamepad,
        "input-tablet" => DeviceType.Pen,
        "phone" => DeviceType.Phone,
        _ => null
    };
}
