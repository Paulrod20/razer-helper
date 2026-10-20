namespace RazerHelper.Core.Hardware;

/// <summary>One Razer laptop this app can look for, by its USB product id.</summary>
/// <param name="Verified">
/// Someone has confirmed the commands in <see cref="RazerCommands"/> actually
/// work on this model. An unverified entry is still tried the same way: a
/// command the firmware does not implement fails cleanly with
/// <see cref="RazerCommandNotSupportedException"/> instead of doing something
/// unexpected.
/// </param>
internal sealed record RazerLaptopModel(int ProductId, string Name, bool Verified);
