using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Models;

namespace RazerHelper.Core.Services;

/// <summary>How a round of closing went: how many apps were asked, and how many were skipped as unsafe or changed.</summary>
internal sealed record DgpuCloseResult(int Asked, int Skipped);

/// <summary>
/// Asks apps to close, and re-checks each one first. Only apps whose verdict
/// is <see cref="DgpuAppVerdict.Close"/>, that are not on a never-close list,
/// and that are still the same program that was scanned (same name and start
/// time, so a reused process id can never point at something else) are asked.
/// Anything doubtful is skipped.
/// </summary>
internal sealed class DgpuAppCloser(IProcessControl processControl)
{
    public DgpuCloseResult Close(IEnumerable<DgpuApp> apps, IReadOnlyCollection<string>? neverClose = null)
    {
        var asked = 0;
        var skipped = 0;

        foreach (var app in apps)
        {
            if (!IsStillSafeToClose(app, neverClose))
            {
                skipped++;
                AppLog.Info($"Not closing {app.Name} (pid {app.ProcessId}): it is no longer a safe match.");
                continue;
            }

            if (processControl.RequestClose(app.ProcessId))
            {
                asked++;
                AppLog.Info($"Asked {app.Name} (pid {app.ProcessId}) to close.");
            }
            else
            {
                skipped++;
                AppLog.Info($"{app.Name} (pid {app.ProcessId}) has no window to close; left running.");
            }
        }

        return new DgpuCloseResult(asked, skipped);
    }

    private bool IsStillSafeToClose(DgpuApp app, IReadOnlyCollection<string>? neverClose)
    {
        if (app.Verdict != DgpuAppVerdict.Close ||
            app.StartTime is not { } scannedStart ||
            DgpuAppSelector.IsProtectedName(app.Name, neverClose))
        {
            return false;
        }

        var identity = processControl.Identify(app.ProcessId);

        return identity is not null &&
            string.Equals(identity.Name, app.Name, StringComparison.OrdinalIgnoreCase) &&
            identity.StartTime == scannedStart;
    }
}
