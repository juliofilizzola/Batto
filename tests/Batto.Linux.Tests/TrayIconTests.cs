using Batto.Linux.Tray;
using Batto.Models;
using SkiaSharp;
using Xunit;

namespace Batto.Linux.Tests;

public class TrayIconTests
{
    [Fact]
    public void RenderPixmap_WritesPreviewForReview()
    {
        var devices = new List<BluetoothDeviceModel>
        {
            Device("X11mouse2", DeviceType.Mouse, true, 76),
            Device("QQ2", DeviceType.Headphones, false, 100),
            Device("AK820 5.1-1", DeviceType.Keyboard, true, 94),
            Device("MX Master 3S", DeviceType.Mouse, true, 19),
        };

        foreach (int size in new[] { 16, 22, 32, 256 })
        {
            byte[] argb = TrayIconRenderer.RenderArgbPixmap(devices, size);
            SavePng(argb, size, Path.Combine(Path.GetTempPath(), $"batto-tray-{size}.png"));
        }
    }

    private static void SavePng(byte[] argb, int size, string path)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        var pixels = bitmap.GetPixelSpan();
        for (int i = 0; i < argb.Length; i += 4)
        {
            pixels[i] = argb[i + 1];
            pixels[i + 1] = argb[i + 2];
            pixels[i + 2] = argb[i + 3];
            pixels[i + 3] = argb[i];
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);
        Assert.True(File.Exists(path));
    }

    private static BluetoothDeviceModel Device(string name, DeviceType type, bool connected, int level) => new()
    {
        Id = "bluez:" + name,
        Name = name,
        Type = type,
        IsConnected = connected,
        BatteryLevel = level,
        HasBatteryInfo = true,
    };
}
