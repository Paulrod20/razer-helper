namespace RazerHelper.Core.Hardware;

/// <summary>Finds other Razer hardware (mice, keyboards, headsets) plugged in alongside the laptop.</summary>
internal static class RazerPeripheralScanner
{
    private const int RazerVendorId = RazerHidTransport.RazerVendorId;

    // The laptop's own control device, which is not a peripheral.
    private const int LaptopProductId = RazerHidTransport.Blade16_2023ProductId;

    /// <summary>The product names of connected Razer devices other than the laptop itself.</summary>
    public static IReadOnlyList<string> FindConnectedNames()
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var path in HidDeviceLocator.FindPaths(RazerVendorId))
            {
                // The laptop's own control device is not a peripheral.
                if (HidDeviceLocator.Matches(path, RazerVendorId, LaptopProductId))
                    continue;

                var name = HidDeviceLocator.GetProductName(path);

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
}
