using RazerHelper.Core.Hardware;

namespace RazerHelper.Tests.Hardware;

public class RazerHidPacketTests
{
    private const int ReportLength = RazerHidPacket.MinimumFeatureReportLength;
    private const byte TransactionId = 0x1F;

    [Fact]
    public void CreateRequest_MatchesTheSynapseCapture_ForSetPerformanceMode()
    {
        // Command 0x0D02 with arguments 01 01 00 00 (a Balanced/Auto write for
        // zone 1). Synapse's own USB capture, published with razer-ctl, shows
        // this packet ending in a 0x0B checksum, so this pins our encoding to
        // bytes a real Razer tool produced.
        var report = RazerHidPacket.CreateRequest(0x0D02, [0x01, 0x01, 0x00, 0x00], ReportLength);

        Assert.Equal(ReportLength, report.Length);
        Assert.Equal(0x00, report[0]);           // HID report id
        Assert.Equal(0x00, report[1]);           // status: new command
        Assert.Equal(TransactionId, report[2]);
        Assert.Equal(4, report[6]);              // argument count
        Assert.Equal(0x0D, report[7]);           // command class
        Assert.Equal(0x02, report[8]);           // command id
        Assert.Equal(new byte[] { 0x01, 0x01, 0x00, 0x00 }, report[9..13]);
        Assert.Equal(0x0B, report[89]);          // checksum
    }

    [Theory]
    [InlineData(0x0D02, new byte[] { 1, 2, 4, 0 })]
    [InlineData(0x0712, new byte[] { 0xD0 })]
    [InlineData(0x0D88, new byte[] { 0, 2, 0 })]
    [InlineData(0x0D07, new byte[] { 1, 1, 3 })]
    public void CreateRequest_ChecksumIsTheXorOfArgumentCountCommandAndArguments(ushort command, byte[] arguments)
    {
        var report = RazerHidPacket.CreateRequest(command, arguments, ReportLength);

        // Computed independently of the production loop: everything from the
        // argument count up to the checksum byte, none of the header.
        var expected = (byte)(arguments.Length ^ (command >> 8) ^ (command & 0xFF));
        foreach (var argument in arguments)
            expected ^= argument;

        Assert.Equal(expected, report[89]);
    }

    [Fact]
    public void CreateRequest_PadsToTheDeviceReportLengthAndKeepsTheChecksumInPlace()
    {
        const int longerReport = 128;

        var report = RazerHidPacket.CreateRequest(0x0D02, [1, 1, 0, 0], longerReport);

        Assert.Equal(longerReport, report.Length);
        Assert.Equal(0x0B, report[89]);
        Assert.All(report[90..], value => Assert.Equal(0, value));
    }

    [Fact]
    public void CreateRequest_AcceptsTheMaximumOfEightyArguments()
    {
        var report = RazerHidPacket.CreateRequest(0x0D02, new byte[80], ReportLength);

        Assert.Equal(80, report[6]);
    }

    [Fact]
    public void CreateRequest_RejectsMoreThanEightyArguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RazerHidPacket.CreateRequest(0x0D02, new byte[81], ReportLength));
    }

    [Fact]
    public void CreateRequest_RejectsAReportShorterThanTheMinimum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RazerHidPacket.CreateRequest(0x0D02, [1], ReportLength - 1));
    }

    [Theory]
    [InlineData(0x02, true)]   // success
    [InlineData(0x00, false)]  // new command
    [InlineData(0x01, false)]  // busy
    [InlineData(0x03, false)]  // failure
    [InlineData(0x04, false)]  // timeout
    [InlineData(0x05, false)]  // not supported
    public void IsMatchingSuccessfulResponse_OnlyAcceptsStatusSuccess(byte status, bool expected)
    {
        var response = Response(status, TransactionId, 0x0D02);

        Assert.Equal(expected, RazerHidPacket.IsMatchingSuccessfulResponse(response, 0x0D02));
    }

    [Fact]
    public void IsMatchingSuccessfulResponse_RejectsAnEchoOfADifferentCommand()
    {
        // A stale reply to an earlier command must not be taken as the answer.
        var response = Response(0x02, TransactionId, 0x0D82);

        Assert.False(RazerHidPacket.IsMatchingSuccessfulResponse(response, 0x0D02));
    }

    [Fact]
    public void IsMatchingSuccessfulResponse_RejectsAnotherTransactionId()
    {
        var response = Response(0x02, 0x22, 0x0D02);

        Assert.False(RazerHidPacket.IsMatchingSuccessfulResponse(response, 0x0D02));
    }

    [Fact]
    public void IsMatchingSuccessfulResponse_RejectsAResponseThatIsTooShort()
    {
        var response = new byte[RazerHidPacket.MinimumFeatureReportLength - 1];

        Assert.False(RazerHidPacket.IsMatchingSuccessfulResponse(response, 0x0D02));
    }

    [Theory]
    [InlineData(0x01, true)]
    [InlineData(0x02, false)]
    [InlineData(0x05, false)]
    public void IsBusyResponse_OnlyRecognisesStatusBusy(byte status, bool expected)
    {
        Assert.Equal(expected, RazerHidPacket.IsBusyResponse(Response(status, TransactionId, 0x0D02)));
    }

    [Fact]
    public void IsNotSupportedResponse_RecognisesStatusFiveForTheSameCommand()
    {
        Assert.True(RazerHidPacket.IsNotSupportedResponse(Response(0x05, TransactionId, 0x0D02), 0x0D02));
    }

    [Fact]
    public void IsNotSupportedResponse_IgnoresAStaleNotSupportedForAnotherCommand()
    {
        // Treating this as "unsupported" would wrongly give up on a command
        // the device never actually refused.
        Assert.False(RazerHidPacket.IsNotSupportedResponse(Response(0x05, TransactionId, 0x0D82), 0x0D02));
    }

    [Fact]
    public void IsNotSupportedResponse_IsFalseForOtherStatuses()
    {
        Assert.False(RazerHidPacket.IsNotSupportedResponse(Response(0x02, TransactionId, 0x0D02), 0x0D02));
        Assert.False(RazerHidPacket.IsNotSupportedResponse(Response(0x03, TransactionId, 0x0D02), 0x0D02));
    }

    [Fact]
    public void GetArgument_ReadsFromTheArgumentAreaStartingAtByteNine()
    {
        var response = new byte[ReportLength];
        response[9] = 0xAA;
        response[10] = 0xBB;
        response[88] = 0xCC;

        Assert.Equal(0xAA, RazerHidPacket.GetArgument(response, 0));
        Assert.Equal(0xBB, RazerHidPacket.GetArgument(response, 1));
        Assert.Equal(0xCC, RazerHidPacket.GetArgument(response, 79));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(80)]
    public void GetArgument_RejectsAnIndexOutsideTheEightyByteArea(int index)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RazerHidPacket.GetArgument(new byte[ReportLength], index));
    }

    private static byte[] Response(byte status, byte transactionId, ushort command)
    {
        var response = new byte[ReportLength];
        response[1] = status;
        response[2] = transactionId;
        response[7] = (byte)(command >> 8);
        response[8] = (byte)command;
        return response;
    }
}
