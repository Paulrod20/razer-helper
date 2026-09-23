using RazerHelper.Core.Hardware;

namespace RazerHelper.Core.Services;

internal sealed class DeviceSupportService
{
    /// <summary>A generic label for use before detection has run, or when nothing was found.</summary>
    public const string GenericModelName = "Razer Blade";

    /// <summary>
    /// The name of the connected model, such as "Razer Blade 16 (2023)", if one
    /// was found. Display controls use plain Windows APIs and work on any
    /// machine; fan telemetry and the battery charge limit need this.
    /// </summary>
    public bool TryGetPresentModelName(out string modelName)
    {
        if (RazerHidTransport.TryGetPresentModel(out var model))
        {
            modelName = model.Name;
            return true;
        }

        modelName = string.Empty;
        return false;
    }
}
