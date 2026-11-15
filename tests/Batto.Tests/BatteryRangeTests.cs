using Batto.Models;
using Xunit;

namespace Batto.Tests;

public class BatteryRangeTests
{
    [Theory]
    [InlineData(100, BatteryFaixa.High)]
    [InlineData(85, BatteryFaixa.High)]
    [InlineData(80, BatteryFaixa.High)]
    [InlineData(79, BatteryFaixa.Medium)]
    [InlineData(50, BatteryFaixa.Medium)]
    [InlineData(40, BatteryFaixa.Medium)]
    [InlineData(39, BatteryFaixa.Low)]
    [InlineData(25, BatteryFaixa.Low)]
    [InlineData(20, BatteryFaixa.Low)]
    [InlineData(19, BatteryFaixa.Critical)]
    [InlineData(10, BatteryFaixa.Critical)]
    [InlineData(0, BatteryFaixa.Critical)]
    [InlineData(-1, BatteryFaixa.Unknown)]
    [InlineData(null, BatteryFaixa.Unknown)]
    public void Test_Faixa_Calculation(int? level, BatteryFaixa expected)
    {
        var result = BatteryRangeHelper.GetFaixa(level);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Test_HexColor_NotNull()
    {
        Assert.Equal("#22C55E", BatteryRangeHelper.GetHexColor(BatteryFaixa.High));
        Assert.Equal("#38BDF8", BatteryRangeHelper.GetHexColor(BatteryFaixa.Medium));
        Assert.Equal("#F59E0B", BatteryRangeHelper.GetHexColor(BatteryFaixa.Low));
        Assert.Equal("#EF4444", BatteryRangeHelper.GetHexColor(BatteryFaixa.Critical));
    }
}
