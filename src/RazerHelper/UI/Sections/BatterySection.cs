using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Services;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// Battery charge-limit slider. Owns the charge-limit service and reports
/// results through events so the host decides how to persist and display them.
/// </summary>
internal sealed class BatterySection : Panel
{
    private const int DefaultLimit = 100;
    private const int LimitStep = 20;
    private const int MinimumLimit = 60;
    private const int MaximumLimit = 100;

    private readonly BatteryChargeLimitService _chargeLimitService = new();
    private readonly ThemedSlider _slider;
    private readonly Label _limitLabel;

    // The limit the EC last confirmed (or the saved one, until the first
    // write). Null means nothing has been applied yet.
    private int? _appliedLimit;
    private bool _updateInProgress;

    public BatterySection(int? savedLimit)
    {
        _appliedLimit = savedLimit;

        var initialLimit = NormalizeLimit(savedLimit ?? DefaultLimit);

        BackColor = BackgroundColor;
        Dock = DockStyle.Fill;
        Margin = new Padding(0, 0, 0, 8);
        Padding = Padding.Empty;

        var header = new TableLayoutPanel
        {
            BackColor = BackgroundColor,
            ColumnCount = 2,
            Dock = DockStyle.Top,
            Height = 28,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 1
        };

        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        var charge = new Label
        {
            AutoSize = true,
            Dock = DockStyle.None,
            Font = CreateDesignFont("Segoe UI", 9.5F),
            ForeColor = Color.Silver,
            Text = "Charge: ",
            TextAlign = ContentAlignment.MiddleRight
        };

        _limitLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.None,
            Font = CreateDesignFont("Segoe UI", 9.5F),
            ForeColor = RazerGreen,
            Margin = new Padding(6, 0, 0, 0),
            Text = $"{initialLimit}%",
            TextAlign = ContentAlignment.MiddleRight
        };

        var batteryValues = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            WrapContents = false
        };

        batteryValues.Controls.Add(charge);
        batteryValues.Controls.Add(_limitLabel);

        header.Controls.Add(CreateSectionLabel("Battery Charge Limit"), 0, 0);
        header.Controls.Add(batteryValues, 1, 0);

        _slider = new ThemedSlider(MinimumLimit, MaximumLimit, LimitStep)
        {
            Dock = DockStyle.Top,
            Value = initialLimit
        };

        _slider.ValueChanged += (_, _) => _limitLabel.Text = $"{_slider.Value}%";
        _slider.Committed += async (_, _) => await CommitAsync();

        var spacer = new Panel
        {
            BackColor = BackgroundColor,
            Dock = DockStyle.Top,
            Height = 5
        };

        Controls.Add(_slider);
        Controls.Add(spacer);
        Controls.Add(header);
    }

    /// <summary>Raised after the EC confirms a new limit.</summary>
    public event EventHandler<int>? ChargeLimitApplied;

    /// <summary>Raised with a user-facing message about the last operation.</summary>
    public event EventHandler<SectionStatus>? StatusChanged;

    /// <summary>Re-applies the saved limit, e.g. after a reboot.</summary>
    public async Task RestoreAsync()
    {
        if (_appliedLimit is not int savedLimit)
            return;

        try
        {
            await _chargeLimitService.SetChargeLimitAsync(NormalizeLimit(savedLimit));
        }
        catch (Exception exception)
        {
            AppLog.Error(
                $"Could not restore the saved battery charge limit ({savedLimit}%).",
                exception);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _chargeLimitService.Dispose();

        base.Dispose(disposing);
    }

    private async Task CommitAsync()
    {
        if (_updateInProgress)
            return;

        var requestedLimit = _slider.Value;
        var previousLimit = _appliedLimit;

        // MouseUp and KeyUp fire for any click or key press, not only when the
        // value moved. The applied limit is only updated after a confirmed
        // write, so matching it means the EC already has this value.
        if (requestedLimit == previousLimit)
            return;

        _updateInProgress = true;
        _slider.Enabled = false;

        try
        {
            await _chargeLimitService.SetChargeLimitAsync(requestedLimit);

            _appliedLimit = requestedLimit;
            ChargeLimitApplied?.Invoke(this, requestedLimit);

            StatusChanged?.Invoke(this, new SectionStatus(requestedLimit == MaximumLimit
                ? "Battery charge limit disabled. Charging is allowed to 100%."
                : $"Battery charge limit set to {requestedLimit}%."));
        }
        catch (Exception exception)
        {
            AppLog.Error(
                $"Battery charge-limit change to {requestedLimit}% failed.",
                exception);

            _slider.Value = NormalizeLimit(previousLimit ?? DefaultLimit);

            StatusChanged?.Invoke(this, new SectionStatus(
                "Could not change the battery charge limit.",
                IsError: true));
        }
        finally
        {
            _slider.Enabled = true;
            _updateInProgress = false;
        }
    }

    private static int NormalizeLimit(int value)
    {
        var clampedValue = Math.Clamp(value, MinimumLimit, MaximumLimit);
        return (int)Math.Round(
            (clampedValue - MinimumLimit) / (double)LimitStep,
            MidpointRounding.AwayFromZero) * LimitStep + MinimumLimit;
    }
}
