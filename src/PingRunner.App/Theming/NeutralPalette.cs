using System.Windows.Media;

namespace PingRunner.App.Theming;

/// <summary>
/// The neutral surfaces and the status colors for one mode. Accents live apart
/// (<see cref="AccentPalette"/>), so a new accent never touches these.
/// </summary>
public sealed record NeutralPalette(
    Color Background,
    Color Surface,
    Color SurfaceRaised,
    Color TextPrimary,
    Color TextSecondary,
    Color Border,
    Color Success,
    Color Warning,
    Color Danger)
{
    public static NeutralPalette Light { get; } = new(
        Background: Color.FromRgb(0xF4, 0xF5, 0xF7),
        Surface: Color.FromRgb(0xFF, 0xFF, 0xFF),
        SurfaceRaised: Color.FromRgb(0xF8, 0xF9, 0xFB),
        TextPrimary: Color.FromRgb(0x1A, 0x1D, 0x23),
        TextSecondary: Color.FromRgb(0x5A, 0x63, 0x70),
        Border: Color.FromRgb(0xE0, 0xE3, 0xE8),
        Success: Color.FromRgb(0x15, 0x80, 0x3D),
        Warning: Color.FromRgb(0xB4, 0x53, 0x09),
        Danger: Color.FromRgb(0xC0, 0x26, 0x1B));

    public static NeutralPalette Dark { get; } = new(
        Background: Color.FromRgb(0x15, 0x17, 0x1B),
        Surface: Color.FromRgb(0x1E, 0x21, 0x27),
        SurfaceRaised: Color.FromRgb(0x26, 0x2A, 0x31),
        TextPrimary: Color.FromRgb(0xE8, 0xEA, 0xED),
        TextSecondary: Color.FromRgb(0x9B, 0xA3, 0xAE),
        Border: Color.FromRgb(0x31, 0x36, 0x3E),
        Success: Color.FromRgb(0x4A, 0xDE, 0x80),
        Warning: Color.FromRgb(0xFB, 0xBF, 0x24),
        Danger: Color.FromRgb(0xF8, 0x71, 0x71));
}
