using Batto.Models;
using Batto.Native;
using Batto.Tray;
using Xunit;

namespace Batto.Tests;

public class IconGeneratorTests
{
    [Theory]
    [InlineData(16)]
    [InlineData(20)]
    [InlineData(24)]
    [InlineData(32)]
    public void CreateDeviceIcon_ReturnsValidHIcon_AndDestroysCleanly(int size)
    {
        var device = new BluetoothDeviceModel
        {
            Id = "test_dev_1",
            Name = "AirPods Pro",
            Type = DeviceType.Headphones,
            IsConnected = true,
            BatteryLevel = 85
        };

        IntPtr hIcon = IconGenerator.CreateDeviceIcon(device, size);
        Assert.NotEqual(IntPtr.Zero, hIcon);

        bool destroyed = NativeMethods.DestroyIcon(hIcon);
        Assert.True(destroyed);
    }

    [Theory]
    [InlineData(DeviceType.Headphones)]
    [InlineData(DeviceType.Mouse)]
    [InlineData(DeviceType.Keyboard)]
    [InlineData(DeviceType.Gamepad)]
    [InlineData(DeviceType.Speaker)]
    [InlineData(DeviceType.Generic)]
    public void CreateDeviceIcon_SupportsAllDeviceTypes(DeviceType type)
    {
        var device = new BluetoothDeviceModel
        {
            Id = $"test_{type}",
            Name = $"Dispositivo {type}",
            Type = type,
            IsConnected = true,
            BatteryLevel = 50
        };

        IntPtr hIcon = IconGenerator.CreateDeviceIcon(device, 24);
        Assert.NotEqual(IntPtr.Zero, hIcon);

        bool destroyed = NativeMethods.DestroyIcon(hIcon);
        Assert.True(destroyed);
    }

    [Fact]
    public void CreateDeviceIcon_HandlesDisconnectedAndNullBattery()
    {
        var deviceDisconnected = new BluetoothDeviceModel
        {
            Id = "test_off",
            Name = "Mouse Desconectado",
            Type = DeviceType.Mouse,
            IsConnected = false,
            BatteryLevel = null
        };

        IntPtr hIcon = IconGenerator.CreateDeviceIcon(deviceDisconnected, 16);
        Assert.NotEqual(IntPtr.Zero, hIcon);
        Assert.True(NativeMethods.DestroyIcon(hIcon));
    }
}
