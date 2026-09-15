using System.Runtime.InteropServices;

namespace RazerHelper.UI.Forms;

public sealed class TrayPopupForm : Form
{
    private static readonly float DpiScale = GetDpiForSystem() / 96F;
    private static readonly Color BackgroundColor = Color.FromArgb(30, 30, 30);
    private static readonly Color ButtonColor = Color.FromArgb(50, 50, 50);
    private static readonly Color BorderColor = Color.FromArgb(80, 80, 80);
    private static readonly Color RazerGreen = Color.FromArgb(68, 214, 44);
    private bool _allowClose;

    public TrayPopupForm()
    {
        // Keep the tray popup's design surface stable across display scales.
        AutoScaleMode = AutoScaleMode.None;

        ApplyTheme();
        BuildView();

        Deactivate += (_, _) => BeginInvoke(HideWhenInactive);
    }

    public void CloseForApplicationExit()
    {
        _allowClose = true;
        Close();
    }

    private void ApplyTheme()
    {
        BackColor = BackgroundColor;
        ForeColor = Color.White;
        Font = CreateDesignFont("Segoe UI", 9F);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(560, 600);
        Text = "RazerHelper";
        StartPosition = FormStartPosition.Manual;
    }

    private void BuildView()
    {
        var content = new TableLayoutPanel
        {
            BackColor = BackgroundColor,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(16, 12, 16, 12),
            RowCount = 7
        };

        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 124F));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 146F));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 104F));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 68F));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));

        content.Controls.Add(CreateAppHeader(), 0, 0);
        content.Controls.Add(CreatePerformanceSection(), 0, 1);
        content.Controls.Add(CreateFanSection(), 0, 2);
        content.Controls.Add(CreateBatterySection(), 0, 3);
        content.Controls.Add(CreateStatusSection(), 0, 4);
        content.Controls.Add(CreateFooter(), 0, 6);

        Controls.Add(content);
    }

    private Control CreateAppHeader()
    {
        var header = CreateTwoColumnLayout(60F, 40F);

        var title = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Left,
            Font = CreateDesignFont("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = RazerGreen,
            Text = "RazerHelper",
            TextAlign = ContentAlignment.MiddleLeft
        };

        var temperatures = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Right,
            Font = CreateDesignFont("Segoe UI", 9.5F),
            ForeColor = Color.Silver,
            Name = "temperatureLabel",
            Text = "CPU: -- C   GPU: -- C",
            TextAlign = ContentAlignment.MiddleRight
        };

        header.Controls.Add(title, 0, 0);
        header.Controls.Add(temperatures, 1, 0);
        return header;
    }

    private Control CreatePerformanceSection()
    {
        var section = CreateSectionPanel();
        var modes = CreateButtonGrid(["Balanced", "Silent", "Custom"], "ModeButton");

        section.Controls.Add(modes);
        section.Controls.Add(CreateSectionHeader("Performance Mode", "Plugged in"));
        return section;
    }

    private Control CreateFanSection()
    {
        var section = CreateSectionPanel();
        var controls = CreateButtonGrid(["Auto", "Max", "Manual"], "FanButton");
        var readings = CreateFanReadings();

        section.Controls.Add(controls);
        section.Controls.Add(readings);
        section.Controls.Add(CreateSectionHeader("Fan Control", string.Empty));
        return section;
    }

    private Control CreateBatterySection()
    {
        var section = CreateSectionPanel();
        var header = new TableLayoutPanel
        {
            BackColor = BackgroundColor,
            ColumnCount = 3,
            Dock = DockStyle.Top,
            Height = 28,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 1
        };

        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));

        var title = CreateSectionLabel("Battery Charge Limit");
        var charge = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Right,
            Font = CreateDesignFont("Segoe UI", 9.5F),
            ForeColor = Color.Silver,
            Name = "batteryStatusLabel",
            Text = "Charge: --%",
            TextAlign = ContentAlignment.MiddleRight
        };
        var limit = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Right,
            Font = CreateDesignFont("Segoe UI", 9.5F),
            ForeColor = RazerGreen,
            Name = "batteryLimitLabel",
            Text = "80%",
            TextAlign = ContentAlignment.MiddleRight
        };

        header.Controls.Add(title, 0, 0);
        header.Controls.Add(charge, 1, 0);
        header.Controls.Add(limit, 2, 0);

        var slider = new TrackBar
        {
            BackColor = BackgroundColor,
            Dock = DockStyle.Bottom,
            LargeChange = 10,
            Maximum = 100,
            Minimum = 50,
            Name = "batteryLimitSlider",
            Height = 46,
            SmallChange = 5,
            TickFrequency = 10,
            Value = 80
        };
        slider.ValueChanged += (_, _) => limit.Text = $"{slider.Value}%";

        section.Controls.Add(slider);
        section.Controls.Add(header);
        return section;
    }

    private Control CreateStatusSection()
    {
        var section = CreateSectionPanel();
        var title = CreateSectionLabel("RazerHelper Status");
        title.Dock = DockStyle.Top;

        var note = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Font = CreateDesignFont("Segoe UI", 9.5F),
            ForeColor = Color.Silver,
            Padding = new Padding(0, 4, 0, 0),
            Text = "Tray shell is ready. Hardware controls are disabled until verified."
        };

        section.Controls.Add(note);
        section.Controls.Add(title);
        return section;
    }

    private Control CreateFooter() => new Label
    {
        AutoSize = true,
        Dock = DockStyle.Left,
        Font = CreateDesignFont("Segoe UI", 8.5F),
        ForeColor = Color.FromArgb(145, 145, 145),
        Text = "RazerHelper  |  Blade 16 (2023)",
        TextAlign = ContentAlignment.MiddleLeft
    };

    private static Panel CreateSectionPanel() => new()
    {
        BackColor = BackgroundColor,
        Dock = DockStyle.Fill,
        Margin = Padding.Empty,
        Padding = Padding.Empty
    };

    private static TableLayoutPanel CreateTwoColumnLayout(float leftWidth, float rightWidth)
    {
        var layout = new TableLayoutPanel
        {
            BackColor = BackgroundColor,
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 1
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, leftWidth));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, rightWidth));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        return layout;
    }

    private static Control CreateSectionHeader(string title, string detail)
    {
        var header = CreateTwoColumnLayout(60F, 40F);
        header.Dock = DockStyle.Top;
        header.Height = 28;

        header.Controls.Add(CreateSectionLabel(title), 0, 0);

        if (!string.IsNullOrWhiteSpace(detail))
        {
            header.Controls.Add(new Label
            {
                AutoSize = true,
                Dock = DockStyle.Right,
                Font = CreateDesignFont("Segoe UI", 9.5F),
                ForeColor = Color.Silver,
                Text = detail,
                TextAlign = ContentAlignment.MiddleRight
            }, 1, 0);
        }

        return header;
    }

    private static Label CreateSectionLabel(string text) => new()
    {
        AutoSize = true,
        Dock = DockStyle.Left,
        Font = CreateDesignFont("Segoe UI", 10F, FontStyle.Bold),
        ForeColor = Color.White,
        Text = text,
        TextAlign = ContentAlignment.MiddleLeft
    };

    private static Control CreateFanReadings()
    {
        var readings = CreateTwoColumnLayout(50F, 50F);
        readings.Dock = DockStyle.Top;
        readings.Height = 24;
        readings.Padding = new Padding(0, 0, 0, 2);

        readings.Controls.Add(CreateReadingLabel("CPU Fan: -- RPM", "cpuFanLabel", DockStyle.Left), 0, 0);
        readings.Controls.Add(CreateReadingLabel("GPU Fan: -- RPM", "gpuFanLabel", DockStyle.Right), 1, 0);
        return readings;
    }

    private static Label CreateReadingLabel(string text, string name, DockStyle dock) => new()
    {
        AutoSize = true,
        Dock = dock,
        Font = CreateDesignFont("Segoe UI", 9.5F),
        ForeColor = Color.Silver,
        Name = name,
        Text = text,
        TextAlign = ContentAlignment.MiddleLeft
    };

    private static Control CreateButtonGrid(IReadOnlyList<string> buttonNames, string nameSuffix)
    {
        var grid = new TableLayoutPanel
        {
            BackColor = BackgroundColor,
            ColumnCount = buttonNames.Count,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 1
        };

        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        for (var index = 0; index < buttonNames.Count; index++)
        {
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / buttonNames.Count));

            var name = buttonNames[index];
            var button = CreateActionButton(name);
            button.Name = $"{name}{nameSuffix}";
            button.Tag = name;
            grid.Controls.Add(button, index, 0);
        }

        return grid;
    }

    private static Button CreateActionButton(string text)
    {
        var button = new Button
        {
            BackColor = ButtonColor,
            Cursor = Cursors.Hand,
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            Font = CreateDesignFont("Segoe UI", 9.5F),
            ForeColor = Color.White,
            Margin = new Padding(4),
            Text = text,
            UseVisualStyleBackColor = false
        };

        button.FlatAppearance.BorderColor = BorderColor;
        button.FlatAppearance.BorderSize = 1;
        return button;
    }

    private void HideWhenInactive()
    {
        if (!_allowClose && Visible && !ContainsFocus)
            Hide();
    }

    private static Font CreateDesignFont(string familyName, float pointSize, FontStyle style = FontStyle.Regular) =>
        new(familyName, pointSize / DpiScale, style, GraphicsUnit.Point);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();

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
