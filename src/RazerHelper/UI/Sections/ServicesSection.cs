using System.ServiceProcess;
using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Hardware;
using RazerHelper.Core.Services;
using RazerHelper.Helpers;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// The bottom row of the popup: how much of Razer's software is running, and a
/// button to stop it (or bring it back). That covers its background services,
/// its own programs (Synapse and helpers) and the entry that starts them at
/// login. The services can contend with this app for the laptop's controller,
/// and stopping them needs administrator rights, so that part relaunches the
/// app elevated for that one action.
/// </summary>
internal sealed class ServicesSection : SectionPanel
{
    // The rule, the spacing under it, the count and button, and the one-line note.
    public const int RowHeight = 70;

    private static readonly TimeSpan HoverRefreshInterval = TimeSpan.FromSeconds(2);

    private readonly RazerSoftwareManager _manager;
    private readonly ThemedToolTip _toolTip = new();
    private readonly Label _countLabel;
    private readonly Button _actionButton;

    private Dictionary<string, ServiceStartMode> _recordedModes;
    private Dictionary<string, string> _recordedLogins;
    private RazerSoftwareStatus? _status;
    private bool _busy;
    private bool _hasServices;
    private DateTime _lastHoverRefresh = DateTime.MinValue;

    public ServicesSection(
        RazerSoftwareManager manager,
        IReadOnlyDictionary<string, ServiceStartMode>? recordedModes,
        IReadOnlyDictionary<string, string>? recordedLogins)
    {
        _manager = manager;
        _recordedModes = new Dictionary<string, ServiceStartMode>(recordedModes ?? new Dictionary<string, ServiceStartMode>());
        _recordedLogins = new Dictionary<string, string>(recordedLogins ?? new Dictionary<string, string>());

        // Last row of the popup, so no gap below it. Hidden until we know
        // Razer's software is installed.
        Margin = Padding.Empty;
        Visible = false;

        _countLabel = new Label
        {
            Dock = DockStyle.Fill,
            Font = CreateDesignFont("Segoe UI", 9.5F),
            ForeColor = Color.Silver,
            Text = "Razer Software Running: --",
            TextAlign = ContentAlignment.MiddleLeft
        };

        // Hovering the number shows what it counts, and re-reads it first so
        // it is current at the moment you look.
        _countLabel.MouseEnter += (_, _) => RefreshOnHover();

        _actionButton = CreateActionButton("Stop");
        _actionButton.Click += ActionButton_Click;

        var row = new TableLayoutPanel
        {
            BackColor = BackgroundColor,
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(0, 8, 0, 0), // Clear space below the rule.
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

    /// <summary>Raised before anything changes, with what each Razer login entry was set to, so it can be saved.</summary>
    public event EventHandler<Dictionary<string, string>>? LoginEntriesRecorded;

    public event EventHandler<SectionStatus>? StatusChanged;

    /// <summary>
    /// Raised while a dialog or the elevation prompt is up. Both take focus
    /// from the popup, which would otherwise hide itself out from under them.
    /// </summary>
    public event EventHandler<bool>? ModalStateChanged;

    public bool HasServices => _hasServices;

    /// <summary>Reads how much of Razer's software is running now.</summary>
    public async Task RefreshAsync()
    {
        if (_busy)
            return;

        RazerSoftwareStatus? status = null;

        try
        {
            status = await Task.Run(_manager.GetStatus).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            AppLog.Error("Could not read the Razer software.", exception);
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
            var outcome = _status.NeedsStop ? await StopAsync() : await StartAsync();

            if (outcome is not null)
                StatusChanged?.Invoke(this, outcome);
        }
        catch (Exception exception)
        {
            AppLog.Error("Changing the Razer software failed.", exception);
            StatusChanged?.Invoke(this, new SectionStatus("Could not change the Razer software.", IsError: true));
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

        // Ask the user first, naming exactly what will change and what that
        // costs. The scan is off the UI thread; the dialog is back on it.
        var peripherals = await Task.Run(RazerPeripheralScanner.FindConnectedNames);

        var answer = MessageBox.Show(
            FindForm(),
            BuildStopWarning(status, peripherals),
            "Stop Razer software",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (answer != DialogResult.Yes)
            return null;

        var problems = new List<string>();

        // The services need administrator rights, so they go through the
        // elevated helper. With nothing left to stop there is no prompt at all.
        if (status.ServicesToStop.Count > 0)
        {
            // Recorded before anything changes, so a crash or a declined prompt
            // can never leave services disabled with no record of what they were.
            _recordedModes = new Dictionary<string, ServiceStartMode>(
                RazerServiceManager.RecordStartModes(status.Services, _recordedModes));
            StartModesRecorded?.Invoke(this, _recordedModes);

            _countLabel.Text = "Stopping Razer software...";
            var result = await ElevatedRunner.RunAsync(RazerServiceCommand.StopArguments());

            if (result.UserDeclined)
                return new SectionStatus("Administrator approval was declined. Nothing was changed.", IsError: true);

            if (result.ExitCode != RazerServiceCommand.Success)
                problems.Add("Some Razer services could not be stopped.");
        }

        // The login entry is the user's own to change, so no prompt. Written
        // down first, then switched off the way Task Manager's Startup tab does.
        var change = _manager.DisableLoginEntries(_recordedLogins, record =>
        {
            _recordedLogins = new Dictionary<string, string>(record);
            LoginEntriesRecorded?.Invoke(this, _recordedLogins);
        });

        if (!change.IsSuccess)
            problems.Add("Razer's startup at login could not be turned off.");

        // Razer's own programs are asked to close, never force-closed. The wait
        // is off the UI thread. Synapse lives in the tray and may ignore this.
        await Task.Run(_manager.AskAppsToClose);

        return problems.Count > 0
            ? new SectionStatus($"{string.Join(" ", problems)} See the log for details.", IsError: true)
            : new SectionStatus("Razer software stopped and kept off.");
    }

    private async Task<SectionStatus?> StartAsync()
    {
        _countLabel.Text = "Starting Razer software...";

        var problems = new List<string>();

        if (_status!.Services.Total > 0)
        {
            var result = await ElevatedRunner.RunAsync(RazerServiceCommand.RestoreArguments(_recordedModes));

            if (result.UserDeclined)
                return new SectionStatus("Administrator approval was declined. Nothing was changed.", IsError: true);

            if (result.ExitCode != RazerServiceCommand.Success)
                problems.Add("Some Razer services could not be restored.");
        }

        var failed = _manager.RestoreLoginEntries(_recordedLogins);

        if (failed.Count == 0)
        {
            // Everything is back as it was, so there is nothing left to remember.
            _recordedLogins = [];
            LoginEntriesRecorded?.Invoke(this, _recordedLogins);
        }
        else
        {
            problems.Add("Razer's startup at login could not be restored.");
        }

        return problems.Count > 0
            ? new SectionStatus($"{string.Join(" ", problems)} See the log for details.", IsError: true)
            : new SectionStatus("Razer software restored.");
    }

    // At most once every couple of seconds, however much the pointer wanders over the label.
    private void RefreshOnHover()
    {
        if (_busy || DateTime.UtcNow - _lastHoverRefresh < HoverRefreshInterval)
            return;

        _lastHoverRefresh = DateTime.UtcNow;
        _ = RefreshAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _toolTip.Dispose();

        base.Dispose(disposing);
    }

    private void ShowStatus(RazerSoftwareStatus status)
    {
        _status = status;
        _countLabel.Text = DescribeCount(status);
        _toolTip.SetToolTip(_countLabel, RazerSoftwareSummary.Describe(status, DateTime.Now));
        _actionButton.Text = status.NeedsStop ? "Stop" : "Start";
        _actionButton.Enabled = !_busy;

        var hasServices = status.IsInstalled;

        if (hasServices == _hasServices)
            return;

        _hasServices = hasServices;
        Visible = hasServices;
        HasServicesChanged?.Invoke(this, hasServices);
    }

    // Services and programs running now, and a hint when Razer would still start at login.
    private static string DescribeCount(RazerSoftwareStatus status) =>
        status.Running > 0 ? $"Razer Software Running: {status.Running}"
        : status.LoginEnabled ? "Razer Software Running: 0 (starts at login)"
        : "Razer Software Running: 0";

    private static string BuildStopWarning(RazerSoftwareStatus status, IReadOnlyList<string> peripherals)
    {
        var lines = new List<string> { "Stop Razer's background software?", string.Empty };

        var services = status.ServicesToStop;

        if (services.Count > 0)
        {
            lines.Add($"These {services.Count} services will be stopped and kept off, including after a restart:");
            lines.AddRange(services.Select(service => $"  \u2022 {service.DisplayName}"));
            lines.Add(string.Empty);
        }

        if (status.RunningApps.Count > 0)
        {
            lines.Add("These Razer programs are running and will be asked to close (nothing is force-closed):");
            lines.AddRange(status.RunningApps.Select(name => $"  \u2022 {name}"));
            lines.Add(string.Empty);
        }

        if (status.LoginEnabled)
        {
            lines.Add("Razer will be stopped from starting when you sign in, the same as switching it off in Task Manager's Startup tab:");
            lines.AddRange(status.LoginEntries.Where(entry => entry.IsEnabled).Select(entry => $"  \u2022 {entry.Name}"));
            lines.Add(string.Empty);
        }

        lines.Add(peripherals.Count > 0
            ? $"Razer devices connected now: {string.Join(", ", peripherals)}."
            : "No other Razer devices are connected right now.");

        lines.Add(
            "While the services are off, Razer-only features can't be configured on those devices " +
            "(button remapping, macros, lighting effects, DPI stages). The devices still work as normal.");
        lines.Add(string.Empty);
        lines.Add("Press Start to bring everything back exactly as it was.");

        if (services.Count > 0)
            lines.Add("Windows will ask for administrator approval.");

        return string.Join(Environment.NewLine, lines);
    }
}
