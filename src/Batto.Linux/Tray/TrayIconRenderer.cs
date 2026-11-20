using Batto.Models;
using SkiaSharp;

namespace Batto.Linux.Tray;

/// <summary>
/// Draws the Batto tray icon: a ring split into one segment per visible device, each filled with its battery level.
/// Same look as the official batto.ico: an opaque grey track, segments in the battery range colours, stroke about
/// a fifth of the diameter. Exported as ARGB32 pixmaps for StatusNotifierItem, one per size.
/// </summary>
public static class TrayIconRenderer
{
    /// <summary>Sizes exported to the tray. Hosts pick the closest one, so every panel gets a sharp ring.</summary>
    public static readonly IReadOnlyList<int> PixmapSizes = [16, 22, 24, 32, 48];

    private const float SegmentGapDegrees = 12f;
    private const float StartAngleDegrees = -90f;
    private const float StrokeRatio = 0.18f;
    private const byte BytesPerPixel = 4;

    /// <summary>Track colour of batto.ico (#3A3A3A), opaque so it reads on light and dark panels.</summary>
    private static readonly SKColor TrackColor = new(0x3A, 0x3A, 0x3A);

    /// <summary>Renders the icon at every tray size.</summary>
    public static IReadOnlyList<(int Size, byte[] Argb)> RenderAllSizes(IReadOnlyList<BluetoothDeviceModel> devices) =>
        PixmapSizes.Select(size => (size, RenderArgbPixmap(devices, size))).ToList();

    /// <summary>Renders the icon and returns the pixels as ARGB, most significant byte first (the SNI wire format).</summary>
    public static byte[] RenderArgbPixmap(IReadOnlyList<BluetoothDeviceModel> devices, int size)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        DrawRing(canvas, devices, size);

        return ToArgbNetworkOrder(bitmap);
    }

    private static void DrawRing(SKCanvas canvas, IReadOnlyList<BluetoothDeviceModel> devices, int size)
    {
        float stroke = size * StrokeRatio;
        float inset = stroke / 2f;
        var box = new SKRect(inset, inset, size - inset, size - inset);

        using var track = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = stroke, Color = TrackColor };

        if (devices.Count == 0)
        {
            canvas.DrawOval(box, track);
            return;
        }

        int count = devices.Count;
        float gap = count > 1 ? SegmentGapDegrees : 0f;
        float segment = (360f - gap * count) / count;

        for (int i = 0; i < count; i++)
        {
            var device = devices[i];
            float start = StartAngleDegrees + gap / 2f + i * (segment + gap);

            canvas.DrawArc(box, start, segment, false, track);

            if (!device.IsConnected || !device.BatteryLevel.HasValue) continue;

            int level = Math.Clamp(device.BatteryLevel.Value, 0, 100);
            if (level <= 0) continue;

            var color = BatteryRangeHelper.GetDrawingColor(device.BatteryFaixa);
            using var fill = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = stroke,
                Color = new SKColor(color.R, color.G, color.B, color.A)
            };
            canvas.DrawArc(box, start, segment * level / 100f, false, fill);
        }
    }

    private static byte[] ToArgbNetworkOrder(SKBitmap bitmap)
    {
        ReadOnlySpan<byte> rgba = bitmap.GetPixelSpan();
        byte[] argb = new byte[rgba.Length];
        for (int i = 0; i < rgba.Length; i += BytesPerPixel)
        {
            argb[i] = rgba[i + 3];
            argb[i + 1] = rgba[i];
            argb[i + 2] = rgba[i + 1];
            argb[i + 3] = rgba[i + 2];
        }
        return argb;
    }
}
