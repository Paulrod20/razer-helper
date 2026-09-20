using System.Runtime.InteropServices;
using RazerHelper.Core.Hardware;
using RazerHelper.Core.Services;

namespace RazerHelper.Tests.Hardware;

public class GpuTemperatureTests
{
    [Theory]
    [InlineData(373u, 37.3)] // What the RTX 4070 in the Blade 16 reported when nvidia-smi said 37.
    [InlineData(10u, 1.0)]
    [InlineData(950u, 95.0)]
    [InlineData(1499u, 149.9)]
    public void TheDriversTenthsOfADegree_BecomeCelsius(uint raw, double expected) =>
        Assert.Equal(expected, D3dkmtGpuTemperature.ToCelsius(raw));

    [Fact]
    public void ZeroMeansNoReading_NotZeroDegrees() =>
        Assert.Null(D3dkmtGpuTemperature.ToCelsius(0)); // The Intel graphics report 0.

    [Theory]
    [InlineData(1500u)]
    [InlineData(4_000_000_000u)] // A garbage value.
    public void ANumberThatIsNotBelievable_IsNotShown(uint raw) =>
        Assert.Null(D3dkmtGpuTemperature.ToCelsius(raw));

    // The structures must match Windows' byte for byte on 64-bit, or the
    // driver would read and write the wrong bytes. These sizes were checked
    // against the values the real GPU returned.
    [Fact]
    public void ThePerformanceDataStructure_Is64Bytes() =>
        Assert.Equal(64, Marshal.SizeOf<D3dkmtGpuTemperature.AdapterPerfData>());

    [Fact]
    public void TheTemperatureFieldSitsWhereWindowsPutsIt() =>
        Assert.Equal(56, (int)Marshal.OffsetOf<D3dkmtGpuTemperature.AdapterPerfData>(nameof(D3dkmtGpuTemperature.AdapterPerfData.Temperature)));

    [Fact]
    public void TheQueryStructure_Is24Bytes() =>
        Assert.Equal(24, Marshal.SizeOf<D3dkmtGpuTemperature.QueryAdapterInfo>());

    [Fact]
    public void TheOpenAndCloseStructures_HaveTheirWindowsSizes()
    {
        Assert.Equal(12, Marshal.SizeOf<D3dkmtGpuTemperature.OpenAdapterFromLuid>());
        Assert.Equal(4, Marshal.SizeOf<D3dkmtGpuTemperature.CloseAdapter>());
    }

    [Fact]
    public void WithNoGpuSource_NothingIsReported() =>
        Assert.Null(new NoGpuTemperature().ReadCelsius());
}
