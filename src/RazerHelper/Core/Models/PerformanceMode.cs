namespace RazerHelper.Core.Models;

/// <summary>
/// Performance modes Razer Synapse offers on the Blade 16 (2023). The values
/// are the EC's wire bytes. The EC knows a few more (Performance, Battery,
/// Hyperboost) that Synapse does not expose on this model, so they are
/// deliberately left out.
/// </summary>
internal enum PerformanceMode : byte
{
    Balanced = 0,
    Custom = 4,
    Silent = 5
}
