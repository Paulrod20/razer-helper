namespace RazerHelper.Core.Hardware;

internal static class RazerHidPacket
{
    public const int MinimumFeatureReportLength = 91;

    private const byte TransactionId = 0x1F;
    private const int ArgumentOffset = 9;
    private const int ChecksumOffset = 89;

    public static byte[] CreateRequest(
        ushort command,
        ReadOnlySpan<byte> arguments,
        int featureReportLength)
    {
        if (arguments.Length > 80)
            throw new ArgumentOutOfRangeException(nameof(arguments));

        if (featureReportLength < MinimumFeatureReportLength)
            throw new ArgumentOutOfRangeException(nameof(featureReportLength));

        var report = new byte[featureReportLength];

        report[0] = 0x00; // HID report ID.
        report[1] = 0x00; // New command.
        report[2] = TransactionId;
        report[6] = (byte)arguments.Length;
        report[7] = (byte)(command >> 8);
        report[8] = (byte)command;
        arguments.CopyTo(report.AsSpan(ArgumentOffset));

        report[ChecksumOffset] = CalculateChecksum(report);
        return report;
    }

    public static bool IsMatchingSuccessfulResponse(
        byte[] response,
        ushort command)
    {
        return response.Length >= MinimumFeatureReportLength &&
               response[1] == 0x02 &&
               response[2] == TransactionId &&
               response[7] == (byte)(command >> 8) &&
               response[8] == (byte)command;
    }

    public static bool IsBusyResponse(byte[] response) =>
        response.Length >= MinimumFeatureReportLength && response[1] == 0x01;

    public static bool IsNotSupportedResponse(
        byte[] response,
        ushort command)
    {
        return response.Length >= MinimumFeatureReportLength &&
               response[1] == 0x05 &&
               response[2] == TransactionId &&
               response[7] == (byte)(command >> 8) &&
               response[8] == (byte)command;
    }

    public static byte GetArgument(byte[] response, int index)
    {
        if (index is < 0 or >= 80)
            throw new ArgumentOutOfRangeException(nameof(index));

        return response[ArgumentOffset + index];
    }

    private static byte CalculateChecksum(byte[] report)
    {
        byte checksum = 0;

        // XOR packet bytes 2 through 87. Indexes include the HID report ID here.
        for (var index = 3; index < ChecksumOffset; index++)
            checksum ^= report[index];

        return checksum;
    }
}
