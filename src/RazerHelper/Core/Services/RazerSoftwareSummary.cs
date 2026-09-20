using System.Text;

namespace RazerHelper.Core.Services;

/// <summary>
/// The plain-language breakdown behind the number in the Razer row, shown when
/// the number is hovered: what is counted, what starts at login, and when it
/// was last checked (so a stale number is easy to spot).
/// </summary>
internal static class RazerSoftwareSummary
{
    public static string Describe(RazerSoftwareStatus status, DateTime checkedAt)
    {
        var text = new StringBuilder();

        text.AppendLine($"{status.Running} running = {status.Services.Running} services + {status.RunningApps.Count} Razer programs");
        text.AppendLine();
        text.AppendLine($"Services: {status.Services.Running} of {status.Services.Total} running");

        var notDisabled = status.ServicesToStop.Count;

        if (notDisabled > 0 && status.Services.Running == 0)
            text.AppendLine($"  ({notDisabled} not yet disabled)");

        if (status.RunningApps.Count == 0)
        {
            text.AppendLine("Programs: none running");
        }
        else
        {
            text.AppendLine("Programs running:");

            foreach (var name in status.RunningApps)
                text.AppendLine($"  {name}");
        }

        text.AppendLine(LoginLine(status));
        text.AppendLine();
        text.Append($"Checked at {checkedAt:HH:mm:ss}");

        return text.ToString();
    }

    private static string LoginLine(RazerSoftwareStatus status)
    {
        if (status.LoginEntries.Count == 0)
            return "Start at login: no Razer entry found";

        var enabled = status.LoginEntries.Where(entry => entry.IsEnabled).Select(entry => entry.Name).ToList();

        return enabled.Count > 0
            ? $"Start at login: ON ({string.Join(", ", enabled)})"
            : "Start at login: off";
    }
}
