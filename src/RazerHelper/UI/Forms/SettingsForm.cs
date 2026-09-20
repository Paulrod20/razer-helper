using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Models;
using RazerHelper.Core.Services;
using RazerHelper.Helpers;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Forms;

/// <summary>
/// The small Settings window. Every checkbox applies as soon as it is ticked,
/// so there is nothing to confirm: the window only has a Close button.
/// </summary>
internal sealed class SettingsForm : Form
{
    private const int ContentWidth = 340;
    private const int HintIndent = 22;

    private readonly IStartupRegistration _startupRegistration;
    private readonly CheckBox _startAtLoginBox;
    private readonly CheckBox _autoSwitchBox;
    private readonly CheckBox _hideWhenClickedAwayBox;
    private readonly CheckBox _alwaysOnTopBox;
    private readonly CheckBox _closeGpuAppsBox;
    private readonly Label _errorLabel;

    private bool _isLoading = true;
    private Form? _anchor;

    public SettingsForm(AppSettings settings, IStartupRegistration startupRegistration)
    {
        _startupRegistration = startupRegistration;

        AutoScaleMode = AutoScaleMode.None;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = BackgroundColor;
        ForeColor = Color.White;
        Font = GetDesignFont("Segoe UI", 9F);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "RazerHelper Settings";

        var layout = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = BackgroundColor,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(16, 14, 16, 14),
            WrapContents = false
        };

        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Font = GetDesignFont("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = RazerGreen,
            Margin = new Padding(0, 0, 0, 10),
            Text = "Settings"
        });

        _startAtLoginBox = AddOption(layout, "Start at login", "Open RazerHelper in the tray when you sign in to Windows.");
        _autoSwitchBox = AddOption(layout, "Switch profile when plugging in or unplugging", "Off keeps whatever mode you are in.");
        _hideWhenClickedAwayBox = AddOption(layout, "Hide when clicking away", "Off keeps the window open until you click the tray icon.");
        _alwaysOnTopBox = AddOption(layout, "Always on top", $"Keeps the window above other windows, including games running in a borderless window. Press {GlobalHotkey.Text} in any program, even a game, to bring this window to the front (again to hide it).");
        _closeGpuAppsBox = AddOption(layout, "Close apps using the dedicated GPU when unplugged", "Saves battery. Asks first, and does nothing while an external display is connected. Use Free up GPU in the footer any time.");

        _errorLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.IndianRed,
            Margin = new Padding(0, 0, 0, 6),
            MaximumSize = new Size(ContentWidth, 0),
            Visible = false
        };
        layout.Controls.Add(_errorLabel);

        layout.Controls.Add(CreateDivider());
        layout.Controls.Add(CreateLink("Razer drivers and support", ExternalLinks.OpenRazerDrivers));
        layout.Controls.Add(CreateLink("Open log folder", ExternalLinks.OpenLogFolder));
        layout.Controls.Add(CreateLink("Reset to defaults...", ConfirmReset, Color.IndianRed));

        var closeButton = CreateActionButton("Close");
        closeButton.Dock = DockStyle.None;
        closeButton.DialogResult = DialogResult.OK;
        closeButton.Margin = new Padding(ContentWidth - 88, 8, 0, 0);
        closeButton.Size = new Size(88, 30);
        layout.Controls.Add(closeButton);

        AcceptButton = closeButton;
        CancelButton = closeButton;
        Controls.Add(layout);

        _autoSwitchBox.Checked = settings.AutoSwitchProfiles;
        _hideWhenClickedAwayBox.Checked = settings.HideWhenClickedAway;
        _alwaysOnTopBox.Checked = settings.AlwaysOnTop;
        _closeGpuAppsBox.Checked = settings.CloseGpuAppsOnUnplug;
        _startAtLoginBox.Checked = ReadStartAtLogin();

        _startAtLoginBox.CheckedChanged += StartAtLoginBox_CheckedChanged;
        _autoSwitchBox.CheckedChanged += (_, _) => AutoSwitchProfilesChanged?.Invoke(this, _autoSwitchBox.Checked);
        _hideWhenClickedAwayBox.CheckedChanged += (_, _) => HideWhenClickedAwayChanged?.Invoke(this, _hideWhenClickedAwayBox.Checked);
        _alwaysOnTopBox.CheckedChanged += (_, _) => AlwaysOnTopChanged?.Invoke(this, _alwaysOnTopBox.Checked);
        _closeGpuAppsBox.CheckedChanged += (_, _) => CloseGpuAppsOnUnplugChanged?.Invoke(this, _closeGpuAppsBox.Checked);

        _isLoading = false;
    }

    public event EventHandler<bool>? AutoSwitchProfilesChanged;

    public event EventHandler<bool>? HideWhenClickedAwayChanged;

    public event EventHandler<bool>? CloseGpuAppsOnUnplugChanged;

    public event EventHandler<bool>? AlwaysOnTopChanged;

    /// <summary>True when the user confirmed a reset; the window then closes and the caller carries it out.</summary>
    public bool ResetConfirmed { get; private set; }

    private bool ReadStartAtLogin()
    {
        try
        {
            return _startupRegistration.IsEnabled;
        }
        catch (Exception exception)
        {
            AppLog.Error("Could not read the start-at-login setting.", exception);
            return false;
        }
    }

    private void StartAtLoginBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_isLoading)
            return;

        var wanted = _startAtLoginBox.Checked;

        try
        {
            _startupRegistration.SetEnabled(wanted);
            _errorLabel.Visible = false;
            AppLog.Info($"Start at login turned {(wanted ? "on" : "off")}.");
        }
        catch (Exception exception)
        {
            AppLog.Error("Could not change the start-at-login setting.", exception);

            // Show what is really true, and say so.
            _isLoading = true;
            _startAtLoginBox.Checked = !wanted;
            _isLoading = false;

            _errorLabel.Text = "Could not change Start at login.";
            _errorLabel.Visible = true;
        }
    }

    // Says exactly what will change before anything does. "No" is the default.
    private void ConfirmReset()
    {
        var answer = MessageBox.Show(
            this,
            "Reset RazerHelper to how it was the first time you opened it?\r\n\r\n" +
            "This will:\r\n" +
            "  - clear your saved settings, including the options in this window and your never-close list\r\n" +
            "  - turn off Start at login\r\n" +
            "  - set the laptop to Balanced mode with no battery charge limit\r\n\r\n" +
            "It will not change Razer's background services or anything else on your PC. RazerHelper will restart.",
            "Reset to defaults",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (answer != DialogResult.Yes)
            return;

        ResetConfirmed = true;
        DialogResult = DialogResult.OK;
        Close();
    }

    private static CheckBox AddOption(FlowLayoutPanel layout, string text, string hint)
    {
        var box = new CheckBox
        {
            AutoSize = true,
            Cursor = Cursors.Hand,
            Font = GetDesignFont("Segoe UI", 9.5F),
            ForeColor = Color.White,
            Margin = new Padding(0, 4, 0, 0),
            Text = text
        };

        layout.Controls.Add(box);
        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Font = GetDesignFont("Segoe UI", 8F),
            ForeColor = SubtleTextColor,
            Margin = new Padding(HintIndent, 0, 0, 6),
            MaximumSize = new Size(ContentWidth - HintIndent, 0),
            Text = hint
        });

        return box;
    }

    private static Control CreateDivider() => new Panel
    {
        BackColor = BorderColor,
        Height = 1,
        Margin = new Padding(0, 6, 0, 8),
        Width = ContentWidth
    };

    private static LinkLabel CreateLink(string text, Action open, Color? color = null)
    {
        var link = new LinkLabel
        {
            ActiveLinkColor = Color.White,
            AutoSize = true,
            Font = GetDesignFont("Segoe UI", 9.5F),
            LinkBehavior = LinkBehavior.HoverUnderline,
            LinkColor = color ?? RazerGreen,
            Margin = new Padding(0, 2, 0, 2),
            Text = text
        };

        link.LinkClicked += (_, _) => open();
        return link;
    }

    /// <summary>Opens next to this window (the popup) instead of centered over it, where it would hide it.</summary>
    public void PlaceBeside(Form anchor)
    {
        StartPosition = FormStartPosition.Manual;
        _anchor = anchor;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        if (_anchor is null)
            return;

        // The size is only final once the layout has run.
        PerformLayout();
        var workingArea = Screen.FromRectangle(_anchor.Bounds).WorkingArea;
        Location = WindowPlacement.Beside(_anchor.Bounds, Size, workingArea);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WindowChrome.Apply(Handle, BorderColor);
    }
}
