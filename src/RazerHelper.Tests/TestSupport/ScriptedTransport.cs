using RazerHelper.Core.Hardware;

namespace RazerHelper.Tests.TestSupport;

/// <summary>A transport whose answers come from a delegate, for faults a full <see cref="FakeEc"/> would never produce.</summary>
internal sealed class ScriptedTransport(Func<ushort, byte[], byte[]> handler) : IRazerTransport
{
    public byte[] Send(ushort command, ReadOnlySpan<byte> arguments) =>
        handler(command, arguments.ToArray());
}
