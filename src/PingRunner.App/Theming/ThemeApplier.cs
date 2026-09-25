using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using PingRunner.Core.Settings;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using ThemeMode = PingRunner.Core.Settings.ThemeMode;

namespace PingRunner.App.Theming;

/// <summary>
/// Applies the theme mode and accent at run time: WPF UI's own theme and accent, so built-in controls
/// match, and Ping Runner's semantic brushes (<c>PR.*Brush</c>), which views read through
/// DynamicResource so a switch shows at once. System mode follows Windows' app light/dark setting
/// and changes with it.
/// </summary>
public sealed class ThemeApplier : IDisposable
{
    private readonly ResourceDictionary resources;
    private ThemeMode mode = ThemeMode.System;
    private AccentChoice accent = AccentChoice.Teal;

    public ThemeApplier(ResourceDictionary resources)
    {
        this.resources = resources;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    /// <summary>After every apply, for anything drawn in code with theme colors.</summary>
    public event EventHandler? Applied;

    public bool IsDark { get; private set; }

    public void Apply(ThemeMode themeMode, AccentChoice accentChoice)
    {
        mode = themeMode;
        accent = accentChoice;
        IsDark = themeMode switch
        {
            ThemeMode.Light => false,
            ThemeMode.Dark => true,
            _ => SystemUsesDarkApps(),
        };

        var neutral = IsDark ? NeutralPalette.Dark : NeutralPalette.Light;
        var palette = AccentPalette.For(accentChoice);
        var accentColor = palette.Accent(IsDark);
        var wpfTheme = IsDark ? ApplicationTheme.Dark : ApplicationTheme.Light;

        ApplicationThemeManager.Apply(wpfTheme, WindowBackdropType.None, false);
        ApplicationAccentColorManager.Apply(accentColor, wpfTheme, false, false);

        Set("Background", neutral.Background);
        Set("Surface", neutral.Surface);
        Set("SurfaceRaised", neutral.SurfaceRaised);
        Set("TextPrimary", neutral.TextPrimary);
        Set("TextSecondary", neutral.TextSecondary);
        Set("Border", neutral.Border);
        Set("Success", neutral.Success);
        Set("Warning", neutral.Warning);
        Set("Danger", neutral.Danger);
        Set("DangerSoft", WithAlpha(neutral.Danger, 0x2A));
        Set("Accent", accentColor);
        Set("AccentSoft", WithAlpha(accentColor, 0x26));
        Set("AccentHover", WithAlpha(accentColor, 0x3D));
        Set("AccentPressed", WithAlpha(accentColor, 0x55));
        Set("OnAccent", palette.TextOnAccent(IsDark));
        Set("Focus", accentColor);

        SetWpfUiAccent(accentColor, palette.TextOnAccent(IsDark), neutral);
        Applied?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

    // WPF UI's theme dictionaries build some control brushes from the accent once, so a window open
    // across a switch kept the old accent (found in GoalMaker). Fresh brushes under the same keys reach
    // those controls, because their templates look the keys up dynamically. Primary buttons get the
    // accent with its own text color in both modes, instead of WPF UI's pale dark-mode variant.
    private void SetWpfUiAccent(Color accent, Color onAccent, NeutralPalette neutral)
    {
        foreach (var key in new[] { "SystemAccentColor", "SystemAccentColorPrimary", "SystemAccentColorSecondary", "SystemAccentColorTertiary" })
        {
            resources[key] = accent;
        }

        foreach (var key in AccentBrushKeys)
        {
            resources[key] = Frozen(accent);
        }

        resources["AccentButtonBackground"] = Frozen(accent);
        resources["AccentButtonBackgroundPointerOver"] = Frozen(Blend(accent, onAccent, 0.12));
        resources["AccentButtonBackgroundPressed"] = Frozen(Blend(accent, onAccent, 0.22));
        resources["AccentButtonForeground"] = Frozen(onAccent);
        resources["AccentButtonForegroundPointerOver"] = Frozen(onAccent);
        resources["AccentButtonForegroundPressed"] = Frozen(onAccent);
        resources["AccentControlElevationBorderBrush"] = Frozen(Colors.Transparent);
        resources["TextOnAccentFillColorPrimary"] = onAccent;
        resources["TextOnAccentFillColorPrimaryBrush"] = Frozen(onAccent);
        // The selected page in the navigation pane sits on a tint of the accent.
        resources["NavigationViewItemBackgroundSelected"] = Frozen(WithAlpha(accent, 0x26));
        resources["NavigationViewItemBackgroundSelectedLeftFluent"] = Frozen(WithAlpha(accent, 0x26));
        resources["ApplicationBackgroundColor"] = neutral.Background;
        resources["ApplicationBackgroundBrush"] = Frozen(neutral.Background);
    }

    // The brushes WPF UI 4.3's Light.xaml and Dark.xaml make from SystemAccentColor{Primary,Secondary,Tertiary}.
    private static readonly string[] AccentBrushKeys =
    [
        "AccentFillColorDefaultBrush",
        "AccentTextFillColorPrimaryBrush",
        "CheckBoxCheckBackgroundFillChecked",
        "ComboBoxBorderBrushFocused",
        "ComboBoxItemPillFillBrush",
        "HyperlinkButtonForeground",
        "ListBoxItemSelectedBackgroundThemeBrush",
        "ListViewItemPillFillBrush",
        "NavigationViewSelectionIndicatorForeground",
        "ProgressBarForeground",
        "ProgressRingForegroundThemeBrush",
        "RadioButtonOuterEllipseCheckedStroke",
        "SliderThumbBackground",
        "TextControlFocusedBorderBrush",
        "ToggleButtonBackgroundChecked",
        "ToggleSwitchStrokeOn",
        "ToggleSwitchFillOn",
    ];

    private static Color Blend(Color from, Color to, double amount) => Color.FromRgb(
        (byte)Math.Round(from.R + ((to.R - from.R) * amount)),
        (byte)Math.Round(from.G + ((to.G - from.G) * amount)),
        (byte)Math.Round(from.B + ((to.B - from.B) * amount)));

    private void Set(string role, Color color)
    {
        resources[$"PR.{role}Color"] = color;
        resources[$"PR.{role}Brush"] = Frozen(color);
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (mode == ThemeMode.System && e.Category == UserPreferenceCategory.General)
        {
            Application.Current?.Dispatcher.BeginInvoke(() => Apply(mode, accent));
        }
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);

    private static bool SystemUsesDarkApps()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
    }
}
