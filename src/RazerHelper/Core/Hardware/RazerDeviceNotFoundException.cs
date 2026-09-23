namespace RazerHelper.Core.Hardware;

/// <summary>
/// No HID device matching any product id in <see cref="RazerLaptopModels"/>
/// exists on this machine, so hardware features cannot work at all.
/// </summary>
internal sealed class RazerDeviceNotFoundException(int vendorId)
    : InvalidOperationException(
        $"No supported Razer laptop was found (VID 0x{vendorId:X4}).");
