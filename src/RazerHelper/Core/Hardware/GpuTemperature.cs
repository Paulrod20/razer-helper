using System.Runtime.InteropServices;
using RazerHelper.Core.Diagnostics;

namespace RazerHelper.Core.Hardware;

/// <summary>Where the dedicated GPU's temperature comes from. An interface so the display can be tested without a GPU.</summary>
internal interface IGpuTemperatureSource
{
    /// <summary>The temperature in degrees Celsius, or null when the GPU does not report one.</summary>
    double? ReadCelsius();
}

/// <summary>
/// Reads the dedicated GPU's temperature from Windows' graphics kernel
/// interface: the same data Task Manager shows. It needs no driver, no
/// vendor library and no administrator rights, and it works for any GPU
/// vendor whose driver reports it. Read-only.
/// </summary>
internal sealed class D3dkmtGpuTemperature : IGpuTemperatureSource
{
    // KMTQAITYPE_ADAPTERPERFDATA in Windows' d3dkmthk.h.
    private const int AdapterPerfDataQuery = 62;

    private long? _adapterLuid;
    private bool _loggedFailure;

    public double? ReadCelsius()
    {
        try
        {
            _adapterLuid ??= DiscreteGpuReader.ReadDiscreteAdapters().FirstOrDefault()?.Luid;

            if (_adapterLuid is not { } luid)
                return null;

            var raw = QueryTemperature(luid);

            // A failed query may mean the adapter changed (a driver reset), so look it up again next time.
            if (raw is null)
                _adapterLuid = null;

            return raw is { } value ? ToCelsius(value) : null;
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException)
        {
            if (!_loggedFailure)
            {
                AppLog.Error("Could not read the GPU temperature.", exception);
                _loggedFailure = true;
            }

            return null;
        }
    }

    /// <summary>
    /// The driver reports tenths of a degree, and 0 when it has no reading
    /// (integrated graphics, a sleeping GPU). Anything not believable is
    /// treated as no reading rather than shown.
    /// </summary>
    internal static double? ToCelsius(uint tenthsOfADegree)
    {
        if (tenthsOfADegree == 0)
            return null;

        var celsius = tenthsOfADegree / 10.0;
        return celsius is > 0 and < 150 ? celsius : null;
    }

    private static uint? QueryTemperature(long luid)
    {
        var open = new OpenAdapterFromLuid
        {
            AdapterLuid = new Luid { Low = (uint)(luid & 0xFFFFFFFF), High = (int)(luid >> 32) }
        };

        if (D3DKMTOpenAdapterFromLuid(ref open) != 0)
            return null;

        var size = Marshal.SizeOf<AdapterPerfData>();
        var buffer = Marshal.AllocHGlobal(size);

        try
        {
            Marshal.StructureToPtr(new AdapterPerfData(), buffer, false);

            var query = new QueryAdapterInfo
            {
                Adapter = open.Adapter,
                Type = AdapterPerfDataQuery,
                Data = buffer,
                Size = (uint)size
            };

            if (D3DKMTQueryAdapterInfo(ref query) != 0)
                return null;

            return Marshal.PtrToStructure<AdapterPerfData>(buffer).Temperature;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);

            var close = new CloseAdapter { Adapter = open.Adapter };
            D3DKMTCloseAdapter(ref close);
        }
    }

    // These mirror Windows' structures byte for byte; the tests pin their sizes.
    [StructLayout(LayoutKind.Sequential)]
    internal struct Luid
    {
        public uint Low;
        public int High;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct OpenAdapterFromLuid
    {
        public Luid AdapterLuid;
        public uint Adapter;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct QueryAdapterInfo
    {
        public uint Adapter;
        public int Type;
        public IntPtr Data;
        public uint Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct AdapterPerfData
    {
        public uint PhysicalAdapterIndex;
        public ulong MemoryFrequency;
        public ulong MaxMemoryFrequency;
        public ulong MaxMemoryFrequencyOC;
        public ulong MemoryBandwidth;
        public ulong PciExpressBandwidth;
        public uint FanRpm;
        public uint Power;
        public uint Temperature;
        public byte PowerStateOverride;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CloseAdapter
    {
        public uint Adapter;
    }

    [DllImport("gdi32.dll")]
    private static extern int D3DKMTOpenAdapterFromLuid(ref OpenAdapterFromLuid data);

    [DllImport("gdi32.dll")]
    private static extern int D3DKMTQueryAdapterInfo(ref QueryAdapterInfo data);

    [DllImport("gdi32.dll")]
    private static extern int D3DKMTCloseAdapter(ref CloseAdapter data);
}

/// <summary>For tests and previews: no temperature to report.</summary>
internal sealed class NoGpuTemperature : IGpuTemperatureSource
{
    public double? ReadCelsius() => null;
}
