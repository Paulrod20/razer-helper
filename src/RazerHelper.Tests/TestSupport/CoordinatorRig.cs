using RazerHelper.Core.Models;
using RazerHelper.Core.Services;

namespace RazerHelper.Tests.TestSupport;

/// <summary>
/// A <see cref="DgpuFreeUpCoordinator"/> wired to fakes the test controls: the
/// power source, what the scan finds, what the user answers, and what gets
/// closed. It records what happened so tests can check it. Nothing here can
/// touch a real process, display or GPU.
/// </summary>
internal sealed class CoordinatorRig : IDisposable
{
    public static readonly DateTime Started = new(2026, 3, 1, 8, 30, 0);

    public CoordinatorRig(bool pluggedIn = true, bool enabled = false)
    {
        Power = new FakePowerSource(pluggedIn);
        Coordinator = new DgpuFreeUpCoordinator(
            Power,
            () =>
            {
                ScanCount++;
                return Task.FromResult(NextScan());
            },
            async (apps, dismissWhenPluggedIn) =>
            {
                Shown.Add(apps);
                DismissFlags.Add(dismissWhenPluggedIn);
                return await Answer();
            },
            apps =>
            {
                Closed.Add(apps);
                return new DgpuCloseResult(apps.Count, 0);
            })
        {
            Enabled = enabled
        };
    }

    public FakePowerSource Power { get; }
    public DgpuFreeUpCoordinator Coordinator { get; }

    public int ScanCount { get; private set; }
    public List<IReadOnlyList<DgpuApp>> Shown { get; } = [];
    public List<bool> DismissFlags { get; } = [];
    public List<IReadOnlyList<DgpuApp>> Closed { get; } = [];

    /// <summary>What each scan returns. By default: no external display, and one app that can be closed (id 10).</summary>
    public Func<DgpuScanResult> NextScan { get; set; } = () => Scan(false, Closable(10));

    /// <summary>The user's answer to the question. By default: yes.</summary>
    public Func<Task<bool>> Answer { get; set; } = () => Task.FromResult(true);

    /// <summary>Unplugs the charger and waits for the automatic run it may start.</summary>
    public Task Unplug()
    {
        Power.Set(false);
        return Coordinator.LastRun;
    }

    public static DgpuApp Closable(int pid, string name = "blender", DateTime? started = null) =>
        new(pid, name, 500L * 1024 * 1024, DgpuAppVerdict.Close, started ?? Started);

    public static DgpuScanResult Scan(bool? externalDisplay, params DgpuApp[] apps) =>
        new(GpuFound: true, externalDisplay, apps);

    public void Dispose() => Coordinator.Dispose();
}
