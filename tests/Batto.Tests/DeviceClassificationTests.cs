using Batto.Bluetooth;
using Batto.Models;
using Xunit;

namespace Batto.Tests;

public class DeviceClassificationTests
{
    [Theory]
    [InlineData("AirPods Pro", DeviceType.Headphones)]
    [InlineData("Sony WH-1000XM5", DeviceType.Headphones)]
    [InlineData("Galaxy Buds2 Pro", DeviceType.Headphones)]
    [InlineData("JBL Tune 510BT", DeviceType.Headphones)]
    [InlineData("Echo Dot-68L", DeviceType.Speaker)]
    [InlineData("JBL Flip 6", DeviceType.Speaker)]
    [InlineData("Logitech MX Master 3S", DeviceType.Mouse)]
    [InlineData("X11mouse2", DeviceType.Mouse)]
    [InlineData("Razer DeathAdder V2 Pro", DeviceType.Mouse)]
    [InlineData("AK820 5.1-1", DeviceType.Keyboard)]
    [InlineData("Keychron K2", DeviceType.Keyboard)]
    [InlineData("Logitech MX Keys", DeviceType.Keyboard)]
    [InlineData("Xbox Wireless Controller", DeviceType.Gamepad)]
    [InlineData("DualSense Wireless Controller", DeviceType.Gamepad)]
    [InlineData("Surface Pen", DeviceType.Pen)]
    [InlineData("Dispositivo Desconhecido", DeviceType.Generic)]
    public void ClassifyDeviceType_ClassifiesCorrectly(string name, DeviceType expectedType)
    {
        var type = BluetoothManager.ClassifyDeviceType(name);
        Assert.Equal(expectedType, type);
    }
}
