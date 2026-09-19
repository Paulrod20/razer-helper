using RazerHelper.Core.Hardware;

namespace RazerHelper.Tests.Hardware;

public class DisplayTopologyTests
{
    [Theory]
    [InlineData(0x80000000u)] // Internal: the value the Blade 16's own panel reports.
    [InlineData(11u)]         // DisplayPort embedded (eDP).
    [InlineData(13u)]         // UDI embedded.
    public void BuiltInPanelWiring_IsInternal(uint technology) =>
        Assert.True(DisplayTopology.IsInternal(technology));

    [Theory]
    [InlineData(5u)]          // HDMI.
    [InlineData(10u)]         // DisplayPort external.
    [InlineData(4u)]          // DVI.
    [InlineData(2u)]          // S-Video, chosen as an arbitrary other type.
    [InlineData(0xFFFFFFFFu)] // "Other" / unknown must count as external.
    [InlineData(0u)]          // HD15 (VGA).
    public void AnythingElse_CountsAsExternal(uint technology) =>
        Assert.False(DisplayTopology.IsInternal(technology));
}
