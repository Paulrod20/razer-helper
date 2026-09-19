namespace RazerHelper.Core.Hardware;

internal static class RazerTransportExtensions
{
    /// <summary>
    /// Sends a write and checks that the EC echoed the arguments it accepted.
    /// Anything else means the write did not take effect, so it is an error
    /// rather than something to assume worked.
    /// </summary>
    public static byte[] SendAndConfirm(
        this IRazerTransport transport,
        ushort command,
        byte[] arguments,
        string description)
    {
        var response = transport.Send(command, arguments);

        for (var index = 0; index < arguments.Length; index++)
        {
            if (RazerHidPacket.GetArgument(response, index) != arguments[index])
                throw new InvalidOperationException($"The Razer Blade did not confirm {description}.");
        }

        return response;
    }
}
