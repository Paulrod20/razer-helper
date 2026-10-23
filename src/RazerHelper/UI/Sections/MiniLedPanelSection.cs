using System.Diagnostics;
using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Models;
using RazerHelper.Core.Services;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

internal sealed class MiniLedPanelSection : SectionPanel
{
    public static int RowHeight => S(100);
    private readonly MiniLedPanelService _service;
    private readonly Dictionary<MiniLedPanelMode, Button> _buttons = [];
    private readonly Label _note;
    private readonly LinkLabel _restart;
    private MiniLedPanelMode? _selected;
    private bool _busy;
    private bool _refreshInProgress;
    private bool _panelDetected;

    public MiniLedPanelSection(MiniLedPanelService service)
    {
        _service = service;
        Visible = false;
        var header = CreateTwoColumnLayout(65F, 35F);
        header.Dock = DockStyle.Top;
        header.Height = S(28);
        header.Controls.Add(CreateSectionLabel("Mini-LED panel mode"), 0, 0);
        _restart = new LinkLabel
        {
            Text = "Restart Windows...", AutoSize = true, Dock = DockStyle.Right,
            LinkColor = RazerGreen, ActiveLinkColor = Color.White,
            Font = GetDesignFont("Segoe UI", 9.5F), TextAlign = ContentAlignment.MiddleRight
        };
        _restart.LinkClicked += async (_, _) => await RestartAsync();
        header.Controls.Add(_restart, 1, 0);
        var grid = CreateButtonGrid(["3840x2400 / 120 Hz", "1920x1200 / 240 Hz"], "PanelModeButton");
        grid.Dock = DockStyle.Top;
        grid.Height = S(42);
        var modes = new[] { MiniLedPanelMode.Uhd120, MiniLedPanelMode.Fhd240 };
        var buttons = grid.Controls.OfType<Button>().ToArray();
        for (var index = 0; index < modes.Length; index++)
        {
            var mode = modes[index];
            _buttons[mode] = buttons[index];
            buttons[index].Click += async (_, _) => await SelectAsync(mode);
        }
        _note = new Label
        {
            Text = "Selection applies after Windows restarts.", Dock = DockStyle.Top,
            Height = S(22), ForeColor = Color.Silver, Font = GetDesignFont("Segoe UI", 9F)
        };
        Controls.Add(_note);
        Controls.Add(grid);
        Controls.Add(header);
    }

    public event EventHandler<bool>? AvailabilityChanged;
    public event EventHandler<bool>? ModalStateChanged;

    public async Task InitializeAsync()
    {
        if (_refreshInProgress || _busy || IsDisposed)
            return;
        _refreshInProgress = true;
        try
        {
            var mode = await _service.GetSelectedModeAsync();
            if (IsDisposed || mode is null)
                return;
            ShowSelection(mode.Value);
            if (!_panelDetected)
            {
                _panelDetected = true;
                Visible = true;
                AvailabilityChanged?.Invoke(this, true);
            }
        }
        catch (Exception exception)
        {
            // Unsupported firmware must not break the ordinary display controls.
            AppLog.Error("Mini-LED panel mode detection unavailable.", exception);
        }
        finally
        {
            _refreshInProgress = false;
        }
    }

    private void ShowSelection(MiniLedPanelMode mode)
    {
        _selected = mode;
        HighlightSelected(_buttons.Values, _buttons[mode]);
    }

    private async Task SelectAsync(MiniLedPanelMode mode)
    {
        if (_busy || _selected == mode)
            return;
        _busy = true;
        ModalStateChanged?.Invoke(this, true);
        try
        {
            var label = mode == MiniLedPanelMode.Uhd120 ? "3840x2400 at 120 Hz" : "1920x1200 at 240 Hz";
            if (MessageBox.Show(FindForm(),
                $"Select {label} for the built-in panel?\n\nThe change takes effect after Windows restarts. You can restart later.",
                "Change Mini-LED panel mode", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;
            SetEnabled(false);
            await _service.SetModeAsync(mode);
            if (IsDisposed)
                return;
            ShowSelection(mode);
            _note.Text = "Panel mode saved. Restart Windows to apply.";
            await ConfirmRestartAsync();
        }
        catch (Exception exception)
        {
            AppLog.Error("Could not select Mini-LED panel mode.", exception);
            if (IsDisposed)
                return;
            MessageBox.Show(FindForm(), exception.Message, "Panel mode could not be confirmed",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            // A failed readback can follow a successful write. Refresh the
            // highlight when possible instead of treating the old value as certain.
            _selected = null;
            HighlightSelected(_buttons.Values, null);
            _note.Text = "Panel selection unknown. Check BIOS before restarting.";
            try
            {
                var actual = await _service.GetSelectedModeAsync();
                if (!IsDisposed && actual is not null)
                {
                    ShowSelection(actual.Value);
                    _note.Text = "Firmware selection read back. Restart required to apply.";
                }
            }
            catch (Exception readException)
            {
                AppLog.Error("Could not recover the Mini-LED panel selection.", readException);
            }
        }
        finally
        {
            if (!IsDisposed)
                SetEnabled(true);
            _busy = false;
            ModalStateChanged?.Invoke(this, false);
        }
    }

    private void SetEnabled(bool enabled)
    {
        foreach (var button in _buttons.Values)
            button.Enabled = enabled;
        _restart.Enabled = enabled && _selected is not null;
    }

    private async Task RestartAsync()
    {
        if (_busy || _selected is null)
            return;
        _busy = true;
        ModalStateChanged?.Invoke(this, true);
        try
        {
            await ConfirmRestartAsync();
        }
        finally
        {
            if (!IsDisposed)
                SetEnabled(true);
            _busy = false;
            ModalStateChanged?.Invoke(this, false);
        }
    }

    // Shared by the successful mode change and the optional restart-later link.
    // Restart failures are reported here so they cannot turn a confirmed panel
    // selection into a "mode could not be confirmed" error in SelectAsync.
    private async Task ConfirmRestartAsync()
    {
        try
        {
            if (MessageBox.Show(FindForm(), "Save your work before continuing.\n\nRestart Windows now to apply the selected panel mode?",
                "Restart Windows", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;
            SetEnabled(false);
            // No /f: apps with unsaved work can block the restart.
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "shutdown.exe"),
                ArgumentList = { "/r", "/t", "0" }, UseShellExecute = false,
                CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden
            }) ?? throw new InvalidOperationException("Windows could not start the restart request.");
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"Windows declined the restart (error {process.ExitCode}). Restart from the Start menu instead.");
        }
        catch (Exception exception)
        {
            AppLog.Error("Could not restart Windows for panel mode change.", exception);
            if (!IsDisposed)
                MessageBox.Show(FindForm(), exception.Message, "Could not restart Windows", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
