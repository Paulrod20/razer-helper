namespace RazerHelper.Core.Hardware;

/// <summary>
/// No HID device with the supported Razer Blade vendor and product ID exists
/// on this machine, so hardware features cannot work at all.
/// </summary>
internal sealed class RazerDeviceNotFoundException(int vendorId, int productId)
    : InvalidOperationException(
        $"No supported Razer Blade was found (VID 0x{vendorId:X4}, PID 0x{productId:X4}).");
