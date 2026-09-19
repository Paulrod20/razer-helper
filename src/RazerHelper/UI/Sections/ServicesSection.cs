using System.ServiceProcess;
using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Hardware;
using RazerHelper.Core.Services;
using RazerHelper.Helpers;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// The bottom row of the popup: how many Razer services are running, and a
/// button to stop them (or bring them back). Razer's background services can
/// contend with this app for the laptop's controller, and stopping them needs
/// administrator rights, so the button relaunches the app elevated for that
/// one action.
/// </summary>
internal sealed class ServicesSection : SectionPanel
{
    // The rule above, the count and button, and the one-line note below.
    public const int RowHeight = 62;

    private readonly RazerServiceManager _manager;
    private readonly Label _countLabel;
    private readonly Button _actionButton;

    private Dictionary<string, ServiceStartMode> _recordedModes;
    private RazerServicesStatus? _status;
    private bool _busy;
    private bool _hasServices;

    public ServicesSection(
        RazerServiceManager manager,
        IReadOnlyDictionary<string, ServiceStartMode>? recordedModes)
    {
        _manager = manager;
        _recordedModes = new Dictionary<string, ServiceStartMode>(recordedModes ?? new Dictionary<string, ServiceStartMode>());

        // Last row of the popup, so no gap below it. Hidden until we know
        // Razer's software is installed.
        Margin = Padding.Empty;
        Visible = false;

        _countLabel = new Label
        {
            Dock = DockStyle.Fill,
            Font = CreateDesignFont("Segoe UI", 9.5F),
            ForeColor = Color.Silver,
            Text = "Razer Services Running: --",
            TextAlign = ContentAlignment.MiddleLeft
        };

        _actionButton = CreateActionButton("Stop");
        _actionButton.Click += ActionButton_Click;

        var row = new TableLayoutPanel
        {
            BackColor = BackgroundColor,
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 1
        };

        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96F));
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        row.Controls.Add(_countLabel, 0, 0);
        row.Controls.Add(_actionButton, 1, 0);

        // A thin rule separates this row from the controls above it.
        var separator = new Panel
        {
            BackColor = BorderColor,
            Dock = DockStyle.Top,
            Height = 1
        };

        // Stopping the services is the light-touch option. Removing Synapse
        // altogether is cleaner, and only the user can decide that.
        var note = new Label
        {
            Dock = DockStyle.Bottom,
            Font = CreateDesignFont("Segoe UI", 8F),
            ForeColor = SubtleTextColor,
            Height = 20,
            Padding = new Padding(4, 0, 0, 0),
            Text = "Tip: uninstall Razer Synapse for the cleanest experience.",
            TextAlign = ContentAlignment.MiddleLeft
        };

        // Dock order: the rule and the note dock first, the row fills the rest.
        Controls.Add(row);
        Controls.Add(note);
        Controls.Add(separator);
    }

    /// <summary>Raised when Razer's software appears or disappears, so the host can show or hide this row.</summary>
    public event EventHandler<bool>? HasServicesChanged;

    /// <summary>Raised before anything changes, with the startup types to restore later, so they can be saved.</summary>
    public event EventHandler<Dictionary<string, ServiceStartMode>>? StartModesRecorded;

    public event EventHandler<SectionStatus>? StatusChanged;

    /// <summary>
    /// Raised while a dialog or the elevation prompt is up. Both take focus
    /// from the popup, which would otherwise hide itself out from under them.
    /// </summary>
    public event EventHandler<bool>? ModalStateChanged;

    public bool HasServices => _hasServices;

    /// <summary>Reads how many Razer services are running now.</summary>
    public async Task RefreshAsync()
    {
        if (_busy)
            return;

        RazerServicesStatus? status = null;

        try
        {
            status = await Task.Run(_manager.GetStatus).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            AppLog.Error("Could not read the Razer services.", exception);
        }

        if (status is not null)
            await PostToUiAsync(() => ShowStatus(status));
    }

    private async void ActionButton_Click(object? sender, EventArgs e) =>
        await RunActionAsync();

    private async Task RunActionAsync()
    {
        if (_busy || _status is null)
            return;

        _busy = true;
        _actionButton.Enabled = false;
        ModalStateChanged?.Invoke(this, true);

        try
        {
            var outcome = _status.Running > 0 ? await StopAsync() : await StartAsync();

            if (outcome is not null)
                StatusChanged?.Invoke(this, outcome);
        }
        catch (Exception exception)
        {
            AppLog.Error("Changing the Razer services failed.", exception);
            StatusChanged?.Invoke(this, new SectionStatus("Could not change the Razer services.", IsError: true));
        }
        finally
        {
            _busy = false;
            ModalStateChanged?.Invoke(this, false);
        }

        await RefreshAsync();
    }

    // Returns null when the user backs out, which is not worth a message.
    private async Task<SectionStatus?> StopAsync()
    {
        var status = _status!;

        // Ask the user first, naming exactly what will stop and what that
        // costs. The scan is off the UI thread; the dialog is back on it.
        var peripherals = await Task.Run(RazerPeripheralScanner.FindConnectedNames);

        var answer = MessageBox.Show(
            FindForm(),
            BuildStopWarning(status, peripherals),
            "Stop Razer services",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (answer != DialogResult.Yes)
            return null;

        // Recorded before anything changes, so a crash or a declined prompt
        // can never leave services disabled with no record of what they were.
        _recordedModes = new Dictionary<string, ServiceStartMode>(
            RazerServiceManager.RecordStartModes(status, _recordedModes));
        StartModesRecorded?.Invoke(this, _recordedModes);

        _countLabel.Text = "Stopping Razer services...";
        var result = await ElevatedRunner.RunAsync(RazerServiceCommand.StopArguments());

        return Describe(result, "Razer services stopped and kept off.", "Some Razer services could not be stopped.");
    }

    private async Task<SectionStatus?> StartAsync()
    {
        _countLabel.Text = "Starting Razer services...";
        var result = await ElevatedRunner.RunAsync(RazerServiceCommand.RestoreArguments(_recordedModes));

        return Describe(result, "Razer services restored.", "Some Razer services could not be restored.");
    }

    private static SectionStatus Describe(ElevatedResult result, string success, string failure)
    {
        if (result.UserDeclined)
            return new SectionStatus("Administrator approval was declined. Nothing was changed.", IsError: true);

        return result.ExitCode == RazerServiceCommand.Success
            ? new SectionStatus(success)
            : new SectionStatus($"{failure} See the log for details.", IsError: true);
    }

    private void ShowStatus(RazerServicesStatus status)
    {
        _status = status;
        _countLabel.Text = $"Razer Services Running: {status.Running}";
        _actionButton.Text = status.Running > 0 ? "Stop" : "Start";
        _actionButton.Enabled = !_busy;

        var hasServices = status.Total > 0;

        if (hasServices == _hasServices)
            return;

        _hasServices = hasServices;
        Visible = hasServices;
        HasServicesChanged?.Invoke(this, hasServices);
    }

    private static string BuildStopWarning(RazerServicesStatus status, IReadOnlyList<string> peripherals)
    {
        var lines = new List<string>
        {
            "Stop and disable all Razer services?",
            string.Empty,
            $"These {status.Total} services will be stopped and kept off, including after a restart:"
        };

        lines.AddRange(status.Services.Select(service => $"  • {service.DisplayName}"));
        lines.Add(string.Empty);

        lines.Add(peripherals.Count > 0
            ? $"Razer devices connected now: {string.Join(", ", peripherals)}."
            : "No other Razer devices are connected right now.");

        lines.Add(
            "While the services are off, Razer-only features can't be configured on those devices " +
            "(button remapping, macros, lighting effects, DPI stages). The devices still work as normal.");
        lines.Add(string.Empty);
        lines.Add("Press Start to bring everything back exactly as it was.");
        lines.Add("Windows will ask for administrator approval.");

        return string.Join(Environment.NewLine, lines);
    }
}
