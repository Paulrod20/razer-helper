using RazerHelper.Core.Hardware;

namespace RazerHelper.Tests.Hardware;

public class RazerLaptopModelsTests
{
    [Fact]
    public void IsNotEmpty()
    {
        Assert.NotEmpty(RazerLaptopModels.Known);
    }

    [Fact]
    public void HasNoDuplicateProductIds()
    {
        var productIds = RazerLaptopModels.Known.Select(model => model.ProductId);

        Assert.Equal(productIds.Distinct().Count(), productIds.Count());
    }

    [Fact]
    public void ListsTheVerifiedBlade16_2023()
    {
        Assert.Contains(
            RazerLaptopModels.Known,
            model => model is { ProductId: 0x029F, Name: "Razer Blade 16 (2023)", Verified: true });
    }
}
