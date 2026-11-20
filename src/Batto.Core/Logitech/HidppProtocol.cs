namespace Batto.Logitech;

/// <summary>
/// Pure helpers for the Logitech HID++ 2.0 protocol (long reports, 20 bytes).
/// Request layout: [0x11, deviceIndex, featureIndex, (function &lt;&lt; 4) | softwareId, params...].
/// Error layout: [0x11, deviceIndex, 0xFF, featureIndex, (function &lt;&lt; 4) | softwareId, errorCode].
/// </summary>
public static class HidppProtocol
{
    public const int VendorId = 0x046D;
    public const byte LongReportId = 0x11;
    public const int LongReportLength = 20;

    /// <summary>Feature index used by IRoot (feature 0x0000) to look up other features.</summary>
    public const byte RootFeatureIndex = 0x00;
    public const byte ErrorFeatureIndex = 0xFF;

    /// <summary>IRoot function 0: getFeature(featureId).</summary>
    public const byte RootGetFeatureFunction = 0x00;

    /// <summary>UNIFIED_BATTERY (0x1004), function 1: getStatus. Byte 4 is the state of charge.</summary>
    public const ushort FeatureUnifiedBattery = 0x1004;
    public const byte BatteryGetStatusFunction = 0x01;

    /// <summary>DEVICE_NAME (0x0005): function 0 returns the name length, function 1 returns name chunks.</summary>
    public const ushort FeatureDeviceName = 0x0005;
    public const byte NameGetCountFunction = 0x00;
    public const byte NameGetCharFunction = 0x01;

    /// <summary>Software id sent with every request, so replies can be told apart from notifications.</summary>
    public const byte SoftwareId = 0x01;

    public static byte[] BuildRequest(byte deviceIndex, byte featureIndex, byte function, params byte[] parameters)
    {
        var report = new byte[LongReportLength];
        report[0] = LongReportId;
        report[1] = deviceIndex;
        report[2] = featureIndex;
        report[3] = (byte)((function << 4) | SoftwareId);
        Array.Copy(parameters, 0, report, 4, Math.Min(parameters.Length, LongReportLength - 4));
        return report;
    }

    /// <summary>True when the report answers the request sent to this device, feature and function.</summary>
    public static bool IsReplyFor(byte[] report, byte deviceIndex, byte featureIndex, byte function)
    {
        if (report.Length < 7 || report[0] != LongReportId || report[1] != deviceIndex) return false;

        if (report[2] == ErrorFeatureIndex)
        {
            return report[3] == featureIndex && (report[4] >> 4) == function;
        }

        return report[2] == featureIndex && (report[3] >> 4) == function;
    }

    public static bool IsError(byte[] report) => report.Length > 2 && report[2] == ErrorFeatureIndex;

    /// <summary>Feature index from an IRoot getFeature reply, or null when the feature is missing.</summary>
    public static byte? ParseFeatureIndex(byte[] reply)
    {
        if (IsError(reply)) return null;
        byte index = reply[4];
        return index == 0 ? null : index;
    }

    /// <summary>State of charge from a UNIFIED_BATTERY getStatus reply, or null when unavailable.</summary>
    public static int? ParseBatteryPercent(byte[] reply)
    {
        if (IsError(reply)) return null;
        int percent = reply[4];
        return percent is >= 0 and <= 100 ? percent : null;
    }

    /// <summary>Number of name characters from a DEVICE_NAME getCount reply.</summary>
    public static int ParseNameLength(byte[] reply) => IsError(reply) ? 0 : reply[4];

    /// <summary>ASCII name characters from a DEVICE_NAME getName reply, up to the end of the chunk.</summary>
    public static string ParseNameChunk(byte[] reply)
    {
        if (IsError(reply)) return string.Empty;

        var chars = new List<char>(16);
        for (int i = 4; i < reply.Length && reply[i] != 0; i++)
        {
            chars.Add((char)reply[i]);
        }
        return new string(chars.ToArray());
    }
}
