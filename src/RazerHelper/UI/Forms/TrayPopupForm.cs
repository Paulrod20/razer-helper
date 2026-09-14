namespace RazerHelper.UI.Forms;

public sealed class TrayPopupForm : Form
{
    private static readonly Color RazerGreen = Color.FromArgb(68, 214, 44);
    private bool _allowClose;

    public TrayPopupForm()
    {
        ConfigureWindow();
        BuildView();

        Deactivate += (_, _) => BeginInvoke(HideWhenInactive);
    }

    public void CloseForApplicationExit()
    {
        _allowClose = true;
        Close();
    }

    private void ConfigureWindow()
    {
        Text = "RazerHelper";
        ClientSize = new Size(410, 278);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = Color.FromArgb(12, 12, 12);
        Font = new Font("Segoe UI", 9F);
    }

    private void BuildView()
    {
        var frame = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(28, 28, 28),
            Padding = new Padding(18)
        };

        var title = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            ForeColor = RazerGreen,
            Location = new Point(18, 16),
            Text = "RAZERHELPER"
        };

        var subtitle = new Label
        {
            AutoSize = true,
            ForeColor = Color.Silver,
            Location = new Point(20, 48),
            Text = "Razer Blade 16 control center"
        };

        var closeButton = CreateButton("x", new Point(356, 12), new Size(32, 30));
        closeButton.Font = new Font("Segoe UI", 12F);
        closeButton.Click += (_, _) => Hide();

        var divider = new Panel
        {
            BackColor = Color.FromArgb(70, 70, 70),
            Location = new Point(18, 78),
            Size = new Size(374, 1)
        };

        var systemTitle = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(20, 97),
            Text = "System status"
        };

        var systemMessage = new Label
        {
            AutoSize = true,
            ForeColor = Color.Silver,
            Location = new Point(20, 123),
            Text = "Tray shell is ready"
        };

        var safetyCard = new Panel
        {
            BackColor = Color.FromArgb(35, 48, 33),
            Location = new Point(18, 155),
            Size = new Size(374, 62)
        };

        var safetyTitle = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = RazerGreen,
            Location = new Point(12, 10),
            Text = "SAFE START"
        };

        var safetyMessage = new Label
        {
            AutoSize = true,
            ForeColor = Color.Gainsboro,
            Location = new Point(12, 31),
            Text = "Hardware commands are disabled until verified."
        };

        safetyCard.Controls.AddRange([safetyTitle, safetyMessage]);

        var footer = new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(145, 145, 145),
            Location = new Point(20, 240),
            Text = "RazerHelper  |  Tray shell"
        };

        frame.Controls.AddRange([title, subtitle, closeButton, divider, systemTitle, systemMessage, safetyCard, footer]);
        Controls.Add(frame);
    }

    private static Button CreateButton(string text, Point location, Size size)
    {
        var button = new Button
        {
            BackColor = Color.FromArgb(28, 28, 28),
            Cursor = Cursors.Hand,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.Gainsboro,
            Location = location,
            Size = size,
            TabStop = false,
            Text = text,
            UseVisualStyleBackColor = false
        };

        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private void HideWhenInactive()
    {
        if (!_allowClose && Visible && !ContainsFocus)
            Hide();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnFormClosing(e);
    }
}
