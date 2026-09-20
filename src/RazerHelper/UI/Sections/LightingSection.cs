using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Models;
using RazerHelper.Core.Services;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// Keyboard backlight and lid logo: an effect drop-down and a brightness
/// slider for each. Always available, on battery or plugged in (the laptop has
/// no power-source rule for lighting), and never part of the power profiles.
/// </summary>
/// <remarks>
/// The laptop is the source of truth. The controls show what it reports when
/// the popup opens and again after every change, so if a change is refused the
/// display goes back to what is really lit instead of showing a wrong choice.
/// </remarks>
internal sealed class LightingSection : SectionPanel
{
    private const int HeaderHeight = 28;
    private const int LineHeight = 34;
    private const int BottomGap = 8;

    /// <summary>The header, the two lines, and the gap that separates this section from the next.</summary>
    public const int RowHeight = HeaderHeight + 2 * LineHeight + BottomGap;

    private static readonly KeyboardEffect[] KeyboardEffects =
        [KeyboardEffect.Off, KeyboardEffect.StaticGreen, KeyboardEffect.Spectrum, KeyboardEffect.Wave, KeyboardEffect.Breathing];

    private static readonly LogoMode[] LogoModes = [LogoMode.Off, LogoMode.On, LogoMode.Breathing];

    private readonly LightingService _lightingService;
    private readonly Line _keyboard;
    private readonly Line _logo;

    private bool _busy;

    public LightingSection(LightingService lightingService)
    {
        _lightingService = lightingService;

        _keyboard = new Line("Keyboard", KeyboardEffects.Select(Describe));
        _logo = new Line("Logo", LogoModes.Select(Describe));

        _keyboard.Effect.SelectionChanged += async (_, _) =>
            await ApplyAsync(
                () => _lightingService.SetKeyboardEffectAsync(KeyboardEffects[_keyboard.Effect.SelectedIndex]),
                "Could not change the keyboard lighting.");
        _keyboard.Brightness.Committed += async (_, _) =>
            await ApplyAsync(
                () => _lightingService.SetKeyboardBrightnessAsync(_keyboard.Brightness.Value),
                "Could not change the keyboard brightness.");

        _logo.Effect.SelectionChanged += async (_, _) =>
            await ApplyAsync(
                () => _lightingService.SetLogoAsync(LogoModes[_logo.Effect.SelectedIndex]),
                "Could not change the logo lighting.");
        _logo.Brightness.Committed += async (_, _) =>
            await ApplyAsync(
                () => _lightingService.SetLogoBrightnessAsync(_logo.Brightness.Value),
                "Could not change the logo brightness.");

        var lines = new TableLayoutPanel
        {
            BackColor = BackgroundColor,
            ColumnCount = 4,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 3
        };

        lines.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
        lines.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        lines.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        lines.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
        lines.RowStyles.Add(new RowStyle(SizeType.Absolute, LineHeight));
        lines.RowStyles.Add(new RowStyle(SizeType.Absolute, LineHeight));
        lines.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Takes the spare height, so the two lines keep their size.

        _keyboard.AddTo(lines, 0);
        _logo.AddTo(lines, 1);

        // Dock order: the header docks first, and the lines fill what is left.
        Controls.Add(lines);
        Controls.Add(CreateSectionHeader("Lighting", string.Empty));
    }

    /// <summary>Raised with a user-facing message about the last operation.</summary>
    public event EventHandler<SectionStatus>? StatusChanged;

    /// <summary>Shows what the laptop's lighting is actually set to.</summary>
    public async Task RefreshAsync()
    {
        if (_busy)
            return;

        _busy = true;

        var state = await ReadStateOrUnknownAsync("Could not read the lighting state.").ConfigureAwait(false);

        await PostToUiAsync(() =>
        {
            ShowState(state);
            EndBusy();
        });
    }

    // Runs one change, then shows what the laptop really did, whatever happened.
    private async Task ApplyAsync(Func<Task> change, string failureMessage)
    {
        if (_busy)
        {
            // Something else is talking to the laptop: put the display back to the truth.
            _ = RefreshAsync();
            return;
        }

        _busy = true;
        SetControlsEnabled(false);

        Exception? failure = null;

        try
        {
            await change().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        var state = await ReadStateOrUnknownAsync("Could not read the lighting state after a change.").ConfigureAwait(false);

        await PostToUiAsync(() =>
        {
            ShowState(state);

            if (failure is not null)
            {
                AppLog.Error(failureMessage, failure);
                StatusChanged?.Invoke(this, new SectionStatus(failureMessage, IsError: true));
            }

            EndBusy();
        });
    }

    private static string Describe(KeyboardEffect effect) =>
        effect == KeyboardEffect.StaticGreen ? "Static green" : effect.ToString();

    private static string Describe(LogoMode mode) => mode == LogoMode.On ? "On" : mode.ToString();

    // A failed read is logged and shown as "unknown", never as an old value.
    private async Task<LightingState> ReadStateOrUnknownAsync(string logMessage)
    {
        try
        {
            return await _lightingService.ReadStateAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            AppLog.Error(logMessage, exception);
            return LightingState.Unknown;
        }
    }

    private void EndBusy()
    {
        _busy = false;
        SetControlsEnabled(true);
    }

    private void SetControlsEnabled(bool enabled)
    {
        _keyboard.Enabled = enabled;
        _logo.Enabled = enabled;
    }

    private void ShowState(LightingState state)
    {
        _keyboard.Show(state.Keyboard is { } effect ? Array.IndexOf(KeyboardEffects, effect) : -1, state.KeyboardBrightness);
        _logo.Show(state.Logo is { } mode ? Array.IndexOf(LogoModes, mode) : -1, state.LogoBrightness);
    }

    /// <summary>One line of the section: a name, an effect drop-down, a brightness slider and its percentage.</summary>
    private sealed class Line
    {
        private readonly string _name;
        private readonly Label _percent;

        public Line(string name, IEnumerable<string> effects)
        {
            _name = name;

            Effect = new DropdownButton(effects.ToArray())
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 4, 3)
            };

            Brightness = new ThemedSlider(LightingBrightness.MinimumPercent, LightingBrightness.MaximumPercent, 5, showLabels: false)
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(4, 0, 0, 0)
            };

            _percent = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                Font = CreateDesignFont("Segoe UI", 9.5F),
                ForeColor = Color.Silver,
                Margin = Padding.Empty,
                Text = "--",
                TextAlign = ContentAlignment.MiddleRight
            };

            // The percentage follows the slider while it is dragged, before anything is sent.
            Brightness.ValueChanged += (_, _) => _percent.Text = $"{Brightness.Value}%";
        }

        public DropdownButton Effect { get; }

        public ThemedSlider Brightness { get; }

        public bool Enabled
        {
            set
            {
                Effect.Enabled = value;
                Brightness.Enabled = value;
            }
        }

        /// <summary>Shows what the laptop reports: an effect (or -1 for none) and a brightness (or null to leave it).</summary>
        public void Show(int effectIndex, int? brightness)
        {
            Effect.Select(effectIndex);

            if (brightness is { } percent)
                Brightness.Value = percent;
        }

        public void AddTo(TableLayoutPanel lines, int row)
        {
            lines.Controls.Add(new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                Font = CreateDesignFont("Segoe UI", 9.5F),
                ForeColor = Color.Silver,
                Margin = new Padding(4, 0, 0, 0),
                Text = _name,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, row);
            lines.Controls.Add(Effect, 1, row);
            lines.Controls.Add(Brightness, 2, row);
            lines.Controls.Add(_percent, 3, row);
        }
    }
}
