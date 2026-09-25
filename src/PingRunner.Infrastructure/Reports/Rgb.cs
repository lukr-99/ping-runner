using System.Globalization;

namespace PingRunner.Infrastructure.Reports;

/// <summary>A color the report writers share, read from "#RRGGBB".</summary>
internal readonly record struct Rgb(byte R, byte G, byte B)
{
    public static Rgb DefaultAccent { get; } = Parse("#0F766E");

    public string Hex => $"#{R:X2}{G:X2}{B:X2}";

    /// <summary>The color, or <see cref="DefaultAccent"/> when the text is not "#RRGGBB".</summary>
    public static Rgb ParseOrDefault(string? text) =>
        text is { Length: 7 } && text[0] == '#' && int.TryParse(text.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)
            ? new Rgb((byte)(value >> 16), (byte)(value >> 8), (byte)value)
            : DefaultAccent;

    /// <summary>Mixed with white: 0 is this color, 1 is white. For light panels in the accent's hue.</summary>
    public Rgb Tint(double amount) => new(Toward(R, amount), Toward(G, amount), Toward(B, amount));

    private static Rgb Parse(string text) =>
        new(
            byte.Parse(text.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(text.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(text.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));

    private static byte Toward(byte channel, double amount) =>
        (byte)Math.Round(channel + ((255 - channel) * Math.Clamp(amount, 0, 1)));
}
