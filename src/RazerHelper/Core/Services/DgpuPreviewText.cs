using System.Text;
using RazerHelper.Core.Models;

namespace RazerHelper.Core.Services;

/// <summary>Plain-language summary of a dedicated GPU scan, for the "show me first" preview.</summary>
internal static class DgpuPreviewText
{
    public static string Build(DgpuScanResult scan)
    {
        if (!scan.GpuFound)
            return "No dedicated GPU was found, so there is nothing to close.";

        var text = new StringBuilder();

        if (scan.ExternalDisplay != false)
        {
            text.AppendLine(scan.ExternalDisplay == true
                ? "An external display is connected. It is driven by the dedicated GPU, so the GPU stays on and nothing would be closed."
                : "Windows could not say whether an external display is connected, so nothing would be closed.");
            text.AppendLine();
        }

        var closable = scan.Apps.Where(app => app.Verdict == DgpuAppVerdict.Close).ToList();

        text.AppendLine(closable.Count == 0
            ? "Apps that would be asked to close: none."
            : "Apps that would be asked to close (they can still ask to save first):");

        foreach (var group in closable.GroupBy(app => app.Name, StringComparer.OrdinalIgnoreCase))
            text.AppendLine($"  • {Describe(group.Key, group.ToList())}");

        var leftAlone = scan.Apps.Where(app => app.Verdict != DgpuAppVerdict.Close).ToList();

        if (leftAlone.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("Left alone (Windows, drivers, background helpers):");
            text.AppendLine("  " + string.Join(", ", leftAlone.Select(app => app.Name).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)));
        }

        return text.ToString().TrimEnd();
    }

    private static string Describe(string name, IReadOnlyList<DgpuApp> processes)
    {
        var megabytes = (long)Math.Round(processes.Sum(app => app.DedicatedBytes) / (1024.0 * 1024.0));
        var count = processes.Count > 1 ? $"{processes.Count} processes, " : string.Empty;

        return $"{name} ({count}{megabytes} MB)";
    }
}
