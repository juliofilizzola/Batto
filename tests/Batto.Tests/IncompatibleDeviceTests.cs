using Batto.Models;
using Xunit;

namespace Batto.Tests;

public class IncompatibleDeviceTests
{
    [Fact]
    public void IncompatibleDevice_DoesNotThrow_AndDisplaysAppropriateText()
    {
        var device = new BluetoothDeviceModel
        {
            Id = "device_no_battery",
            Name = "Bluetooth Dongle / Adapter",
            Type = DeviceType.Generic,
            IsConnected = true,
            BatteryLevel = null,
            HasBatteryInfo = false,
            BatterySource = string.Empty
        };

        Assert.Null(device.BatteryLevel);
        Assert.False(device.HasBatteryInfo);
        Assert.Equal("N/D", device.BatteryText);
        Assert.Equal(BatteryFaixa.Unknown, device.BatteryFaixa);
        Assert.Equal("#9CA3AF", device.BatteryColorHex);
    }
}
