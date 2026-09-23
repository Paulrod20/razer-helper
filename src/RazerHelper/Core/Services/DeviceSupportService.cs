using System.Diagnostics.CodeAnalysis;
using RazerHelper.Core.Hardware;

namespace RazerHelper.Core.Services;

internal sealed class DeviceSupportService
{
    /// <summary>A generic label for use before detection has run, or when nothing was found.</summary>
    public const string GenericModelName = "Razer Blade";

    /// <summary>
    /// The connected model, if one was found. Display controls use plain
    /// Windows APIs and work on any machine; fan telemetry and the battery
    /// charge limit need this.
    /// </summary>
    public bool TryGetPresentModel([NotNullWhen(true)] out RazerLaptopModel? model) =>
        RazerHidTransport.TryGetPresentModel(out model);
}
