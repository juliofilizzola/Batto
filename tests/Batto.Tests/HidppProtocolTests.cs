using Batto.Logitech;

namespace Batto.Tests;

public class HidppProtocolTests
{
    [Fact]
    public void BuildRequest_SetsReportIdDeviceFeatureAndFunction()
    {
        byte[] request = HidppProtocol.BuildRequest(0x02, 0x08, 0x01);

        Assert.Equal(HidppProtocol.LongReportLength, request.Length);
        Assert.Equal(0x11, request[0]);
        Assert.Equal(0x02, request[1]);
        Assert.Equal(0x08, request[2]);
        Assert.Equal(0x11, request[3]); // function 1 in the high nibble, software id 1 in the low nibble
    }

    [Fact]
    public void BuildRequest_CopiesParametersAfterHeader()
    {
        byte[] request = HidppProtocol.BuildRequest(0xFF, 0x00, 0x00, 0x10, 0x04);

        Assert.Equal(0x10, request[4]);
        Assert.Equal(0x04, request[5]);
        Assert.Equal(0x00, request[6]);
    }

    [Fact]
    public void IsReplyFor_AcceptsMatchingReply()
    {
        byte[] reply = Reply(devIdx: 0x02, featureIdx: 0x08, function: 0x01, payload: 80);

        Assert.True(HidppProtocol.IsReplyFor(reply, 0x02, 0x08, 0x01));
    }

    [Fact]
    public void IsReplyFor_RejectsOtherDeviceFeatureOrFunction()
    {
        byte[] reply = Reply(devIdx: 0x02, featureIdx: 0x08, function: 0x01, payload: 80);

        Assert.False(HidppProtocol.IsReplyFor(reply, 0x03, 0x08, 0x01));
        Assert.False(HidppProtocol.IsReplyFor(reply, 0x02, 0x09, 0x01));
        Assert.False(HidppProtocol.IsReplyFor(reply, 0x02, 0x08, 0x00));
    }

    [Fact]
    public void IsReplyFor_AcceptsErrorForSameFeatureAndFunction()
    {
        byte[] error = new byte[HidppProtocol.LongReportLength];
        error[0] = 0x11;
        error[1] = 0x02;
        error[2] = 0xFF;
        error[3] = 0x08;
        error[4] = 0x11;
        error[5] = 0x05;

        Assert.True(HidppProtocol.IsError(error));
        Assert.True(HidppProtocol.IsReplyFor(error, 0x02, 0x08, 0x01));
    }

    [Fact]
    public void ParseBatteryPercent_ReturnsStateOfCharge()
    {
        Assert.Equal(80, HidppProtocol.ParseBatteryPercent(Reply(0x02, 0x08, 0x01, payload: 80)));
        Assert.Equal(0, HidppProtocol.ParseBatteryPercent(Reply(0x02, 0x08, 0x01, payload: 0)));
        Assert.Equal(100, HidppProtocol.ParseBatteryPercent(Reply(0x02, 0x08, 0x01, payload: 100)));
    }

    [Fact]
    public void ParseBatteryPercent_RejectsOutOfRangeAndErrors()
    {
        Assert.Null(HidppProtocol.ParseBatteryPercent(Reply(0x02, 0x08, 0x01, payload: 101)));
        Assert.Null(HidppProtocol.ParseBatteryPercent(new byte[] { 0x11, 0x02, 0xFF, 0x08, 0x11, 0x05 }));
    }

    [Fact]
    public void ParseFeatureIndex_ReturnsIndexOrNullWhenMissing()
    {
        Assert.Equal((byte?)0x08, HidppProtocol.ParseFeatureIndex(Reply(0x02, 0x00, 0x00, payload: 0x08)));
        Assert.Null(HidppProtocol.ParseFeatureIndex(Reply(0x02, 0x00, 0x00, payload: 0x00)));
        Assert.Null(HidppProtocol.ParseFeatureIndex(new byte[] { 0x11, 0x02, 0xFF, 0x00, 0x01, 0x02 }));
    }

    [Fact]
    public void ParseNameChunk_StopsAtNullByte()
    {
        byte[] reply = { 0x11, 0x02, 0x09, 0x11, (byte)'M', (byte)'X', (byte)' ', (byte)'3', 0x00, 0x00 };

        Assert.Equal("MX 3", HidppProtocol.ParseNameChunk(reply));
    }

    [Fact]
    public void ParseNameLength_ReadsCountFromByteFour()
    {
        Assert.Equal(12, HidppProtocol.ParseNameLength(Reply(0x02, 0x0A, 0x00, payload: 12)));
    }

    private static byte[] Reply(byte devIdx, byte featureIdx, byte function, byte payload)
    {
        var report = new byte[HidppProtocol.LongReportLength];
        report[0] = HidppProtocol.LongReportId;
        report[1] = devIdx;
        report[2] = featureIdx;
        report[3] = (byte)((function << 4) | HidppProtocol.SoftwareId);
        report[4] = payload;
        return report;
    }
}
