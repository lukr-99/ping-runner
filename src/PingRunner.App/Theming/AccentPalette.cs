using System.Windows.Media;
using PingRunner.Core.Settings;

namespace PingRunner.App.Theming;

/// <summary>
/// One accent in both modes: a deeper shade on light surfaces and a brighter one on dark, each with
/// the text color that stays readable on it.
/// </summary>
public sealed record AccentPalette(AccentChoice Choice, string Name, Color OnLight, Color OnLightText, Color OnDark, Color OnDarkText)
{
    private static readonly Color White = Colors.White;
    private static readonly Color Ink = Color.FromRgb(0x0B, 0x12, 0x15);

    public static IReadOnlyList<AccentPalette> All { get; } =
    [
        new(AccentChoice.Teal, "Teal", Color.FromRgb(0x0F, 0x76, 0x6E), White, Color.FromRgb(0x2D, 0xD4, 0xBF), Ink),
        new(AccentChoice.Blue, "Blue", Color.FromRgb(0x25, 0x63, 0xEB), White, Color.FromRgb(0x60, 0xA5, 0xFA), Ink),
        new(AccentChoice.Violet, "Violet", Color.FromRgb(0x6D, 0x28, 0xD9), White, Color.FromRgb(0xA7, 0x8B, 0xFA), Ink),
        new(AccentChoice.Amber, "Amber", Color.FromRgb(0xB4, 0x53, 0x09), White, Color.FromRgb(0xFB, 0xBF, 0x24), Ink),
    ];

    public static AccentPalette For(AccentChoice choice) => All.FirstOrDefault(accent => accent.Choice == choice) ?? All[0];

    public Color Accent(bool dark) => dark ? OnDark : OnLight;

    public Color TextOnAccent(bool dark) => dark ? OnDarkText : OnLightText;
}
