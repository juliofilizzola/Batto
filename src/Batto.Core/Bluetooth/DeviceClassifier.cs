using Batto.Models;

namespace Batto.Bluetooth;

/// <summary>Guesses the device type from its name. Pure string rules, shared by every platform.</summary>
public static class DeviceClassifier
{
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
}
