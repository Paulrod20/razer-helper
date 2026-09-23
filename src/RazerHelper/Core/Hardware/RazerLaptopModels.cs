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

        // Community-reported product ids, not verified on this app: the
        // command set is the one documented for the Blade 16 (2023) above,
        // tried the same way on these (see RazerCommands). A command the
        // firmware does not implement fails cleanly rather than guessing.
        // From sqmagellan/razer-ctl's published device-support table.
        new RazerLaptopModel(0x028A, "Razer Blade 15 (2022)", Verified: false),
        new RazerLaptopModel(0x029D, "Razer Blade 14 (2023) Mercury", Verified: false),
        new RazerLaptopModel(0x02B7, "Razer Blade 16 (2024)", Verified: false),
        new RazerLaptopModel(0x02C6, "Razer Blade 16 (2025)", Verified: false),
    ];
}
