using Batto.Bluetooth;
using Xunit;

namespace Batto.Tests;

public class PnpReaderTests
{
    [Theory]
    [InlineData((byte)85, 85)]
    [InlineData((int)100, 100)]
    [InlineData((int)0, 0)]
    [InlineData(120, 100)] // Clamped to 100
    [InlineData(-10, 0)]   // Clamped to 0
    [InlineData("75%", 75)]
    [InlineData("42", 42)]
    public void ParseBatteryValue_ParsesCorrectly(object input, int expected)
    {
        var result = PnpBatteryReader.ParseBatteryValue(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Bluetooth#Bluetoothe8:48:b8:c8:20:00-c4:33:96:0b:ef:d0", "C433960BEFD0")]
    [InlineData("Bluetooth#Bluetoothe8:48:b8:c8:20:00-84:ac:60:2a:91:59", "84AC602A9159")]
    [InlineData("BluetoothLE#BluetoothLEe8:48:b8:c8:20:00-dc:56:13:53:f5:b8", null)]
    public void ExtractClassicAddress_ReturnsRemoteAddressForClassicIdsOnly(string id, string? expected)
    {
        Assert.Equal(expected, Batto.Bluetooth.PnpBatteryReader.ExtractClassicAddress(id));
    }

    [Theory]
    [InlineData("BluetoothLE#BluetoothLEe8:48:b8:c8:20:00-dc:56:13:53:f5:b8", "DC561353F5B8")]
    [InlineData("Bluetooth#Bluetoothe8:48:b8:c8:20:00-c4:33:96:0b:ef:d0", null)]
    public void ExtractLeAddress_ReturnsRemoteAddressForBleIdsOnly(string id, string? expected)
    {
        Assert.Equal(expected, Batto.Bluetooth.PnpBatteryReader.ExtractLeAddress(id));
    }
}
