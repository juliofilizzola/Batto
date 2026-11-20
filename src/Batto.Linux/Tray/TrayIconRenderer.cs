using Batto.Models;
using SkiaSharp;

namespace Batto.Linux.Tray;

/// <summary>
/// Draws the Batto tray icon: a ring split into one segment per visible device, each filled with its battery level.
/// Same layout as the Windows IconGenerator, rendered with SkiaSharp and exported as the ARGB32 pixmap of StatusNotifierItem.
/// </summary>
public static class TrayIconRenderer
{
    private const float SegmentGapDegrees = 12f;
    private const float StartAngleDegrees = -90f;
    private const byte TrackAlpha = 70;
    private const byte BytesPerPixel = 4;
    private const float ThickStroke = 4.5f;
    private const float MediumStroke = 3.8f;
    private const float ThinStroke = 3.2f;
    private const int ThickStrokeMinSize = 24;
    private const int MediumStrokeMinSize = 20;

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
        float stroke = size >= ThickStrokeMinSize ? ThickStroke : (size >= MediumStrokeMinSize ? MediumStroke : ThinStroke);
        float inset = stroke / 2f + 0.5f;
        var box = new SKRect(inset, inset, size - inset, size - inset);

        using var track = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = stroke, Color = new SKColor(255, 255, 255, TrackAlpha) };

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
