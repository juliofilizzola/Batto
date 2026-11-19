// Spike: read Logitech battery through HID++ 2.0 (read-only requests).
// Feature 0x0000 (IRoot) finds the feature index of 0x1004 (UNIFIED_BATTERY),
// then function 1 (GetStatus) returns the state of charge in byte 4.
using System;
using System.Linq;
using System.Threading;
using HidSharp;

const int LogitechVid = 0x046D;
const byte LongReport = 0x11;
const byte SwId = 0x01;
// Device index 0xFF = the receiver/device itself (Bluetooth direct); 1..6 = paired slots on a receiver.
byte[] deviceIndexes = { 0xFF, 1, 2, 3, 4, 5, 6 };

var devices = DeviceList.Local.GetHidDevices(LogitechVid)
    .Where(d => d.GetMaxOutputReportLength() >= 20)
    .ToList();

Console.WriteLine($"Logitech HID++ candidates: {devices.Count}");
foreach (var dev in devices)
{
    Console.WriteLine($"\n== {dev.GetProductName()}  (PID {dev.ProductID:X4})  {dev.DevicePath}");
    HidStream stream;
    try
    {
        if (!dev.TryOpen(out stream))
        {
            Console.WriteLine("  could not open");
            continue;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  open failed: {ex.Message}");
        continue;
    }

    using (stream)
    {
        stream.ReadTimeout = 800;
        foreach (byte idx in deviceIndexes)
        {
            byte? featureIndex = GetFeatureIndex(stream, idx, 0x1004);
            if (featureIndex is null)
            {
                Console.WriteLine($"  devIdx {idx:X2}: no response or feature 0x1004 missing");
                continue;
            }

            int? percent = GetBatteryPercent(stream, idx, featureIndex.Value);
            Console.WriteLine($"  devIdx {idx:X2}: feature 0x1004 at index {featureIndex:X2}, battery = {(percent is null ? "n/a" : percent + "%")}");
        }
    }
}

Console.WriteLine("\nDone.");

static byte? GetFeatureIndex(HidStream stream, byte devIdx, ushort featureId)
{
    // IRoot (index 0x00), function 0 = getFeature(featureId)
    byte[]? reply = Request(stream, devIdx, 0x00, 0x00, (byte)(featureId >> 8), (byte)(featureId & 0xFF));
    if (reply is null) return null;
    // Error responses carry 0xFF in byte 2
    if (reply[2] == 0xFF) return null;
    byte index = reply[4];
    return index == 0 ? null : index;
}

static int? GetBatteryPercent(HidStream stream, byte devIdx, byte featureIndex)
{
    // UNIFIED_BATTERY, function 1 = getStatus
    byte[] reply = Request(stream, devIdx, featureIndex, 0x01);
    if (reply is null || reply[2] == 0xFF) return null;
    return reply[4];
}

static byte[]? Request(HidStream stream, byte devIdx, byte featureIndex, byte function, params byte[] parameters)
{
    var report = new byte[20];
    report[0] = LongReport;
    report[1] = devIdx;
    report[2] = featureIndex;
    report[3] = (byte)((function << 4) | SwId);
    Array.Copy(parameters, 0, report, 4, Math.Min(parameters.Length, 16));

    try
    {
        stream.Write(report);
    }
    catch
    {
        return null;
    }

    // Wait for the reply that matches this device index and feature index (or an error for them)
    var buffer = new byte[20];
    var deadline = DateTime.UtcNow.AddMilliseconds(800);
    while (DateTime.UtcNow < deadline)
    {
        int read;
        try
        {
            read = stream.Read(buffer, 0, buffer.Length);
        }
        catch (TimeoutException)
        {
            return null;
        }
        if (read < 7 || buffer[0] != LongReport) continue;
        if (buffer[1] != devIdx) continue;

        bool isError = buffer[2] == 0xFF && buffer[3] == featureIndex;
        bool isMatch = buffer[2] == featureIndex && (buffer[3] >> 4) == function;
        if (isError || isMatch) return (byte[])buffer.Clone();
    }
    return null;
}
