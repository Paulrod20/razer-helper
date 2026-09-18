namespace RazerHelper.Core.Hardware;

/// <summary>
/// The device answered, but reported that it does not implement the command.
/// The connection itself is healthy.
/// </summary>
internal sealed class RazerCommandNotSupportedException(ushort command)
    : InvalidOperationException(
        $"The Razer device does not support command 0x{command:X4}.");
