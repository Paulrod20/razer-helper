namespace RazerHelper.Core.Hardware;

/// <summary>
/// The command channel to the laptop's embedded controller. Services depend on
/// this rather than on HID directly, so their logic can run against a fake in
/// tests without any hardware.
/// </summary>
internal interface IRazerTransport
{
    /// <summary>Sends one command and returns the device's matching response.</summary>
    byte[] Send(ushort command, ReadOnlySpan<byte> arguments);
}
