using RazerHelper.Core.Models;

namespace RazerHelper.UI.Sections;

/// <summary>A profile the user just changed, and which power source it belongs to.</summary>
internal sealed record PowerProfileChange(bool PluggedIn, PowerProfile Profile);
