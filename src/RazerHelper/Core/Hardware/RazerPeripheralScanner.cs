using HidSharp;

namespace RazerHelper.Core.Hardware;

/// <summary>Finds other Razer hardware (mice, keyboards, headsets) plugged in alongside the laptop.</summary>
internal static class RazerPeripheralScanner
{
    private const int RazerVendorId = 0x1532;

    // The laptop's own control device, which is not a peripheral.
    private const int LaptopProductId = 0x029F;

    /// <summary>The product names of connected Razer devices other than the laptop itself.</summary>
    public static IReadOnlyList<string> FindConnectedNames()
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var device in DeviceList.Local.GetHidDevices(RazerVendorId))
            {
                if (device.ProductID == LaptopProductId)
                    continue;

                var name = TryGetName(device);

                if (!string.IsNullOrWhiteSpace(name))
                    names.Add(name);
            }
        }
        catch (Exception)
        {
            // The list is only there to warn the user; never let it block the action.
        }

        return names.ToList();
    }

    private static string? TryGetName(HidDevice device)
    {
        try
        {
            return device.GetProductName();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
