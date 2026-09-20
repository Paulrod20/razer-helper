using RazerHelper.Core.Services;

namespace RazerHelper.Tests.TestSupport;

/// <summary>A power source the test controls: plug and unplug on demand, and raise the event like Windows does.</summary>
internal sealed class FakePowerSource(bool? pluggedIn = true) : IPowerSource
{
    public bool? IsPluggedIn { get; private set; } = pluggedIn;

    public event EventHandler? PowerSourceChanged;

    public void Set(bool? pluggedIn)
    {
        IsPluggedIn = pluggedIn;
        PowerSourceChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Windows also raises the event for battery percentage changes, with the same source.</summary>
    public void RaiseWithoutChange() => PowerSourceChanged?.Invoke(this, EventArgs.Empty);
}
