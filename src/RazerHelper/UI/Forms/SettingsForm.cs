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
    private readonly Label _errorLabel;
    private readonly LinkLabel _previewLink;

    private bool _isLoading = true;

    public SettingsForm(AppSettings settings, IStartupRegistration startupRegistration)
    {
        _startupRegistration = startupRegistration;

        AutoScaleMode = AutoScaleMode.None;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = BackgroundColor;
        ForeColor = Color.White;
        Font = CreateDesignFont("Segoe UI", 9F);
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
            Font = CreateDesignFont("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = RazerGreen,
            Margin = new Padding(0, 0, 0, 10),
            Text = "Settings"
        });

        _startAtLoginBox = AddOption(layout, "Start at login", "Open RazerHelper in the tray when you sign in to Windows.");
        _autoSwitchBox = AddOption(layout, "Switch profile when plugging in or unplugging", "Off keeps whatever mode you are in.");
        _hideWhenClickedAwayBox = AddOption(layout, "Hide when clicking away", "Off keeps the window open until you click the tray icon.");

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
        _previewLink = CreateLink("Show apps using the dedicated GPU", () => _ = ShowGpuPreviewAsync());
        layout.Controls.Add(_previewLink);
        layout.Controls.Add(CreateLink("Razer drivers and support", ExternalLinks.OpenRazerDrivers));
        layout.Controls.Add(CreateLink("Open log folder", ExternalLinks.OpenLogFolder));

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
        _startAtLoginBox.Checked = ReadStartAtLogin();

        _startAtLoginBox.CheckedChanged += StartAtLoginBox_CheckedChanged;
        _autoSwitchBox.CheckedChanged += (_, _) => AutoSwitchProfilesChanged?.Invoke(this, _autoSwitchBox.Checked);
        _hideWhenClickedAwayBox.CheckedChanged += (_, _) => HideWhenClickedAwayChanged?.Invoke(this, _hideWhenClickedAwayBox.Checked);

        _isLoading = false;
    }

    public event EventHandler<bool>? AutoSwitchProfilesChanged;

    public event EventHandler<bool>? HideWhenClickedAwayChanged;

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

    // A look only: nothing is closed or changed. It shows what the dedicated
    // GPU is being used for, and which of those apps the app would ask to close.
    private async Task ShowGpuPreviewAsync()
    {
        if (!_previewLink.Enabled)
            return;

        _previewLink.Enabled = false;

        try
        {
            var scan = await DgpuScanner.ScanAsync();

            MessageBox.Show(
                this,
                DgpuPreviewText.Build(scan),
                "Apps using the dedicated GPU",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            AppLog.Error("Could not scan the dedicated GPU.", exception);
            _errorLabel.Text = "Could not check the dedicated GPU.";
            _errorLabel.Visible = true;
        }
        finally
        {
            _previewLink.Enabled = true;
        }
    }

    private static CheckBox AddOption(FlowLayoutPanel layout, string text, string hint)
    {
        var box = new CheckBox
        {
            AutoSize = true,
            Cursor = Cursors.Hand,
            Font = CreateDesignFont("Segoe UI", 9.5F),
            ForeColor = Color.White,
            Margin = new Padding(0, 4, 0, 0),
            Text = text
        };

        layout.Controls.Add(box);
        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Font = CreateDesignFont("Segoe UI", 8F),
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

    private static LinkLabel CreateLink(string text, Action open)
    {
        var link = new LinkLabel
        {
            ActiveLinkColor = Color.White,
            AutoSize = true,
            Font = CreateDesignFont("Segoe UI", 9.5F),
            LinkBehavior = LinkBehavior.HoverUnderline,
            LinkColor = RazerGreen,
            Margin = new Padding(0, 2, 0, 2),
            Text = text
        };

        link.LinkClicked += (_, _) => open();
        return link;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WindowChrome.Apply(Handle, BorderColor);
    }
}
