using Batto.Linux.Bluez;
using Batto.Models;
using Xunit;

namespace Batto.Linux.Tests;

public class BlueZIdsTests
{
    [Fact]
    public void DeviceIdFor_UsesUpperCaseAddressWithPrefix()
    {
        Assert.Equal("bluez:C4:33:96:0B:EF:D0", BlueZIds.DeviceIdFor("c4:33:96:0b:ef:d0"));
    }

    [Fact]
    public void AddressFromId_RoundTripsBlueZIds()
    {
        string id = BlueZIds.DeviceIdFor("D7:40:54:FA:3E:8D");
        Assert.Equal("D7:40:54:FA:3E:8D", BlueZIds.AddressFromId(id));
    }

    [Fact]
    public void AddressFromId_ReturnsNullForOtherSourcesIds()
    {
        Assert.Null(BlueZIds.AddressFromId("logitech:C52B:1"));
    }

    [Fact]
    public void ToNumericAddress_ParsesSixBytesAsOneNumber()
    {
        Assert.Equal(0xC433960BEFD0UL, BlueZIds.ToNumericAddress("C4:33:96:0B:EF:D0"));
    }

    [Fact]
    public void ToNumericAddress_ReturnsZeroForMalformedAddress()
    {
        Assert.Equal(0UL, BlueZIds.ToNumericAddress("not-an-address"));
    }
}

public class BlueZIconMapperTests
{
    [Theory]
    [InlineData("audio-headset", DeviceType.Headset)]
    [InlineData("audio-headphones", DeviceType.Headphones)]
    [InlineData("input-mouse", DeviceType.Mouse)]
    [InlineData("input-keyboard", DeviceType.Keyboard)]
    [InlineData("input-gaming", DeviceType.Gamepad)]
    public void Map_KnownIconsMapToDeviceType(string icon, DeviceType expected)
    {
        Assert.Equal(expected, BlueZIconMapper.Map(icon));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("some-unknown-icon")]
    public void Map_UnknownOrMissingIconReturnsNull(string? icon)
    {
        Assert.Null(BlueZIconMapper.Map(icon));
    }
}
