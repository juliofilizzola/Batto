using System.Drawing;
using System.Drawing.Drawing2D;
using Batto.Models;
using Batto.Native;

namespace Batto.Tray;

/// <summary>
/// Draws the single Batto tray icon: a ring split into one segment per device.
/// Each segment fills clockwise with that device's battery level, in the colour of its range.
/// Names and exact values are shown in the hover flyout.
/// </summary>
public static class IconGenerator
{
    private const float SegmentGapDegrees = 12f;

    public static int GetCurrentTrayIconSize()
    {
        int size = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSMICON);
        if (size <= 0) size = 16;
        // Keep within standard bounds 16, 20, 24, 32
        return size switch
        {
            <= 18 => 16,
            <= 22 => 20,
            <= 28 => 24,
            _ => 32
        };
    }

    public static IntPtr CreateSummaryIcon(IReadOnlyList<BluetoothDeviceModel> devices, int? customSize = null)
    {
        int size = customSize ?? GetCurrentTrayIconSize();
        using var bitmap = RenderSummaryBitmap(devices, size);
        return bitmap.GetHicon();
    }

    /// <summary>Single-device ring, kept for callers that render one device.</summary>
    public static IntPtr CreateDeviceIcon(BluetoothDeviceModel device, int? customSize = null)
    {
        return CreateSummaryIcon(new[] { device }, customSize);
    }

    public static Bitmap RenderSummaryBitmap(IReadOnlyList<BluetoothDeviceModel> devices, int size)
    {
        var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        float stroke = size >= 24 ? 4.5f : (size >= 20 ? 3.8f : 3.2f);
        float inset = stroke / 2f + 0.5f;
        var box = new RectangleF(inset, inset, size - 2 * inset, size - 2 * inset);

        if (devices.Count == 0)
        {
            // Idle: faint full ring so the icon is still visible with no devices
            using var idlePen = new Pen(Color.FromArgb(70, 255, 255, 255), stroke);
            g.DrawEllipse(idlePen, box);
            return bitmap;
        }

        int count = devices.Count;
        float gap = count > 1 ? SegmentGapDegrees : 0f;
        float segment = (360f - gap * count) / count;

        using var trackPen = new Pen(Color.FromArgb(70, 255, 255, 255), stroke);

        for (int i = 0; i < count; i++)
        {
            var device = devices[i];
            // Start at 12 o'clock, offset by half a gap so the first segment is centred
            float start = -90f + gap / 2f + i * (segment + gap);

            g.DrawArc(trackPen, box, start, segment);

            if (!device.IsConnected || !device.BatteryLevel.HasValue) continue;

            int level = Math.Clamp(device.BatteryLevel.Value, 0, 100);
            if (level <= 0) continue;

            Color color = BatteryRangeHelper.GetDrawingColor(device.BatteryFaixa);
            using var fillPen = new Pen(color, stroke);
            g.DrawArc(fillPen, box, start, segment * level / 100f);
        }

        return bitmap;
    }

    public static Bitmap RenderDeviceBitmap(BluetoothDeviceModel device, int size)
    {
        return RenderSummaryBitmap(new[] { device }, size);
    }
}
