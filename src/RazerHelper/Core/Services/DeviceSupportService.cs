using RazerHelper.Core.Hardware;

namespace RazerHelper.Core.Services;

public sealed class DeviceSupportService
{
    public const string SupportedModelName = "Razer Blade 16 (2023)";

    /// <summary>
    /// Whether the hardware this app controls is present. Display controls
    /// use plain Windows APIs and work on any machine; fan telemetry and the
    /// battery charge limit need the Razer control interface.
    /// </summary>
    public bool IsSupportedDevicePresent() =>
        RazerHidTransport.IsSupportedDevicePresent();
}
