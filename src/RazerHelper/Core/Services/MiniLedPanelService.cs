using RazerHelper.Core.Hardware;
using RazerHelper.Core.Models;

namespace RazerHelper.Core.Services;

internal sealed class MiniLedPanelService(IRazerTransport transport, Func<int?>? productId = null)
{
    private const int Blade16ProductId = 0x029F;
    private const byte MiniLedCapability = 0x02;
    private readonly Lock _sync = new();

    // Restrict the new commands to the model whose vendor wrapper and readback
    // were checked. A matching PID alone is insufficient: it also ships IPS panels.
    private bool HasSupportedPanel()
    {
        var id = productId is not null ? productId() :
            RazerHidTransport.TryGetPresentModel(out var model) ? model.ProductId : (int?)null;
        return id == Blade16ProductId &&
            (ReadByte(RazerCommands.GetSkuHardwareConfiguration) & MiniLedCapability) != 0;
    }

    public Task<MiniLedPanelMode?> GetSelectedModeAsync() => Task.Run(() =>
    {
        lock (_sync)
        {
            return HasSupportedPanel() ? ReadMode() : (MiniLedPanelMode?)null;
        }
    });

    public Task SetModeAsync(MiniLedPanelMode mode)
    {
        if (mode is not (MiniLedPanelMode.Uhd120 or MiniLedPanelMode.Fhd240))
            throw new ArgumentOutOfRangeException(nameof(mode));

        return Task.Run(() =>
        {
            lock (_sync)
            {
                if (!HasSupportedPanel())
                    throw new NotSupportedException("A supported dual-mode Mini-LED panel was not detected.");

                if (ReadMode() == mode)
                    return;

                // The vendor setter does not promise an argument echo. Confirm
                // the saved selection with its getter instead; never infer that
                // Windows has already switched resolution or refresh rate.
                transport.Send(RazerCommands.SetMiniLedPanelResolution, [(byte)mode]);
                if (ReadMode() != mode)
                    throw new InvalidOperationException("The laptop did not confirm the panel mode. Check the selection in BIOS before restarting.");
            }
        });
    }

    private MiniLedPanelMode ReadMode() => ReadByte(RazerCommands.GetMiniLedPanelResolution) switch
    {
        0 => MiniLedPanelMode.Uhd120,
        1 => MiniLedPanelMode.Fhd240,
        _ => throw new InvalidOperationException("The laptop returned an unknown Mini-LED panel mode.")
    };

    private byte ReadByte(ushort command)
    {
        var response = transport.Send(command, [0x00]);
        if (response.Length < RazerHidPacket.MinimumFeatureReportLength || response[6] < 1)
            throw new InvalidOperationException("The laptop returned an incomplete panel response.");
        return RazerHidPacket.GetArgument(response, 0);
    }
}
