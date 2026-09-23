namespace RazerHelper.Core.Hardware;

/// <summary>
/// Every Razer laptop this app knows how to find. To add one: plug it in with
/// nothing else Razer-branded attached, open Device Manager, find the
/// keyboard-adjacent HID device under this vendor, and read the product id
/// (the four hex digits after "PID_") from its Hardware Ids. It is safe to
/// add an unverified entry; see <see cref="RazerLaptopModel.Verified"/>.
/// </summary>
internal static class RazerLaptopModels
{
    public static readonly IReadOnlyList<RazerLaptopModel> Known =
    [
        new RazerLaptopModel(0x029F, "Razer Blade 16 (2023)", Verified: true),
    ];
}
