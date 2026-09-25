using System.Globalization;

namespace PingRunner.App.Formatting;

/// <summary>How Ping Runner writes its measurements: milliseconds, rates, sizes, shares and spans.</summary>
public static class Units
{
    public const string None = "–";

    private static CultureInfo Culture => CultureInfo.CurrentCulture;

    /// <summary>One decimal below 100 ms ("23.4 ms"), whole numbers above ("312 ms").</summary>
    public static string Milliseconds(double? value) => value switch
    {
        null => None,
        < 100 => string.Format(Culture, "{0:0.0} ms", value),
        _ => string.Format(Culture, "{0:0} ms", value),
    };

    public static string Percent(double? fraction) => fraction switch
    {
        null => None,
        0 => "0 %",
        < 0.001 => string.Format(Culture, "{0:0.00} %", fraction * 100),
        _ => string.Format(Culture, "{0:0.0} %", fraction * 100),
    };

    public static string BitsPerSecond(double? bitsPerSecond) => bitsPerSecond switch
    {
        null => None,
        >= 1e9 => string.Format(Culture, "{0:0.00} Gbps", bitsPerSecond / 1e9),
        >= 1e8 => string.Format(Culture, "{0:0} Mbps", bitsPerSecond / 1e6),
        >= 1e6 => string.Format(Culture, "{0:0.0} Mbps", bitsPerSecond / 1e6),
        _ => string.Format(Culture, "{0:0} kbps", bitsPerSecond / 1e3),
    };

    /// <summary>The number alone, in Mbps, for large readouts.</summary>
    public static string MegabitsNumber(double? bitsPerSecond) => bitsPerSecond switch
    {
        null => None,
        >= 1e8 => string.Format(Culture, "{0:0}", bitsPerSecond / 1e6),
        _ => string.Format(Culture, "{0:0.0}", bitsPerSecond / 1e6),
    };

    public static string Bytes(long bytes) => bytes switch
    {
        >= 1_000_000_000 => string.Format(Culture, "{0:0.00} GB", bytes / 1e9),
        >= 1_000_000 => string.Format(Culture, "{0:0} MB", bytes / 1e6),
        >= 1_000 => string.Format(Culture, "{0:0} kB", bytes / 1e3),
        _ => string.Format(Culture, "{0} B", bytes),
    };

    /// <summary>"4.2 s", "3 min 12 s", "2 h 05 min".</summary>
    public static string Span(TimeSpan span) => span switch
    {
        { TotalSeconds: < 60 } => string.Format(Culture, "{0:0.0} s", span.TotalSeconds),
        { TotalHours: < 1 } => string.Format(Culture, "{0} min {1:00} s", (int)span.TotalMinutes, span.Seconds),
        _ => string.Format(Culture, "{0} h {1:00} min", (int)span.TotalHours, span.Minutes),
    };

    /// <summary>"00:12:31" style, for a running clock.</summary>
    public static string Clock(TimeSpan span) =>
        string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}", (int)span.TotalHours, span.Minutes, span.Seconds);

    public static string Count(int count) => count.ToString("N0", Culture);

    public static string Count(long count) => count.ToString("N0", Culture);

    /// <summary>"1 speed test", "12 speed tests".</summary>
    public static string CountOf(long count, string one, string many) => $"{Count(count)} {(count == 1 ? one : many)}";

    public static string LinkSpeed(long? bitsPerSecond) => bitsPerSecond switch
    {
        null => None,
        >= 1_000_000_000 => string.Format(Culture, "{0:0.#} Gbit/s", bitsPerSecond / 1e9),
        _ => string.Format(Culture, "{0:0} Mbit/s", bitsPerSecond / 1e6),
    };
}
