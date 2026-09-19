using Microsoft.Win32;

namespace RazerHelper.Core.Services;

internal sealed class PowerSourceService : IPowerSource, IDisposable
{
    public PowerSourceService()
    {
        SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
    }

    public bool? IsPluggedIn => SystemInformation.PowerStatus.PowerLineStatus switch
    {
        PowerLineStatus.Online => true,
        PowerLineStatus.Offline => false,
        _ => null
    };

    public event EventHandler? PowerSourceChanged;

    private void SystemEvents_PowerModeChanged(
        object? sender,
        PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.StatusChange)
        {
            PowerSourceChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
    }
}
