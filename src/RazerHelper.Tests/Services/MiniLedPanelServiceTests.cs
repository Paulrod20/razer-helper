using RazerHelper.Core.Hardware;
using RazerHelper.Core.Models;
using RazerHelper.Core.Services;
using RazerHelper.Tests.TestSupport;

namespace RazerHelper.Tests.Services;

public class MiniLedPanelServiceTests
{
    private static byte[] Reply(byte value, byte size = 1)
    {
        var response = new byte[91];
        response[6] = size;
        response[9] = value;
        return response;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task ReadsFirmwareSelection(byte value)
    {
        var service = new MiniLedPanelService(new ScriptedTransport((command, args) =>
        {
            Assert.Equal(new byte[] { 0 }, args);
            return command switch
            {
                0x0D8F => Reply(3),
                0x0D8E => Reply(value),
                _ => throw new Exception("Unexpected write")
            };
        }), () => 0x029F);
        Assert.Equal((MiniLedPanelMode)value, await service.GetSelectedModeAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0x02B7)]
    [InlineData(0x0255)]
    public async Task OtherModelsSendNoCommands(int? product)
    {
        var service = new MiniLedPanelService(new ScriptedTransport((_, _) => throw new Exception("Unexpected command")), () => product);
        Assert.Null(await service.GetSelectedModeAsync());
        await Assert.ThrowsAsync<NotSupportedException>(() => service.SetModeAsync(MiniLedPanelMode.Fhd240));
    }

    [Fact]
    public async Task IpsSkuCannotWritePanelMode()
    {
        var service = new MiniLedPanelService(new ScriptedTransport((command, _) =>
        {
            Assert.Equal(0x0D8F, command);
            return Reply(1); // DDS present, Mini-LED absent.
        }), () => 0x029F);
        Assert.Null(await service.GetSelectedModeAsync());
        await Assert.ThrowsAsync<NotSupportedException>(() => service.SetModeAsync(MiniLedPanelMode.Fhd240));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public async Task WritesVendorByteAndConfirmsWithReadback(byte initial, byte target)
    {
        var current = initial;
        var calls = new List<ushort>();
        var service = new MiniLedPanelService(new ScriptedTransport((command, args) =>
        {
            calls.Add(command);
            switch (command)
            {
                case 0x0D8F: return Reply(3);
                case 0x0D8E: return Reply(current);
                case 0x0D0E:
                    Assert.Equal(new[] { target }, args);
                    current = args[0];
                    return Reply(0); // Setter need not echo the request.
                default: throw new Exception("Unexpected command");
            }
        }), () => 0x029F);
        await service.SetModeAsync((MiniLedPanelMode)target);
        Assert.Equal(new ushort[] { 0x0D8F, 0x0D8E, 0x0D0E, 0x0D8E }, calls);
    }

    [Fact]
    public async Task SameSelectionSendsNoWrite()
    {
        var service = new MiniLedPanelService(new ScriptedTransport((command, _) => command switch
        {
            0x0D8F => Reply(3),
            0x0D8E => Reply(1),
            _ => throw new Exception("Unexpected write")
        }), () => 0x029F);
        await service.SetModeAsync(MiniLedPanelMode.Fhd240);
    }

    [Fact]
    public async Task UnconfirmedWriteFails()
    {
        var service = new MiniLedPanelService(new ScriptedTransport((command, _) =>
            Reply(command == 0x0D8F ? (byte)3 : (byte)0)), () => 0x029F);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetModeAsync(MiniLedPanelMode.Fhd240));
    }

    [Fact]
    public async Task InvalidSelectionSendsNothing()
    {
        var service = new MiniLedPanelService(new ScriptedTransport((_, _) => throw new Exception("Unexpected command")), () => 0x029F);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SetModeAsync((MiniLedPanelMode)2));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(255)]
    public async Task UnknownFirmwareValueFails(byte value)
    {
        var service = new MiniLedPanelService(new ScriptedTransport((command, _) =>
            Reply(command == 0x0D8F ? (byte)3 : value)), () => 0x029F);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetSelectedModeAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task IncompleteRepliesFail(bool truncated)
    {
        var service = new MiniLedPanelService(new ScriptedTransport((_, _) => truncated ? new byte[10] : Reply(3, 0)), () => 0x029F);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetSelectedModeAsync());
    }

    [Fact]
    public async Task CapabilityIsRecheckedBeforeWriting()
    {
        var supported = true;
        var service = new MiniLedPanelService(new ScriptedTransport((command, _) => command switch
        {
            0x0D8F => Reply(supported ? (byte)3 : (byte)1),
            0x0D8E => Reply(1),
            _ => throw new Exception("Unexpected write after capability changed")
        }), () => 0x029F);

        Assert.Equal(MiniLedPanelMode.Fhd240, await service.GetSelectedModeAsync());
        supported = false;
        await Assert.ThrowsAsync<NotSupportedException>(() => service.SetModeAsync(MiniLedPanelMode.Uhd120));
    }

    [Fact]
    public async Task UnknownSelectionPreventsWriting()
    {
        var service = new MiniLedPanelService(new ScriptedTransport((command, _) => command switch
        {
            0x0D8F => Reply(3),
            0x0D8E => Reply(255),
            _ => throw new Exception("Unexpected write with unknown current selection")
        }), () => 0x029F);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetModeAsync(MiniLedPanelMode.Uhd120));
    }

    [Fact]
    public async Task UnsupportedFirmwareDoesNotWrite()
    {
        var service = new MiniLedPanelService(new ScriptedTransport((command, _) =>
            throw new RazerCommandNotSupportedException(command)), () => 0x029F);
        await Assert.ThrowsAsync<RazerCommandNotSupportedException>(() => service.GetSelectedModeAsync());
        await Assert.ThrowsAsync<RazerCommandNotSupportedException>(() => service.SetModeAsync(MiniLedPanelMode.Fhd240));
    }
}
