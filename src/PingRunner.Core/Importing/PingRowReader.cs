using System.Globalization;
using System.Text.RegularExpressions;
using PingRunner.Core.Pinging;

namespace PingRunner.Core.Importing;

/// <summary>
/// Turns rows of text into pings, whatever the column order or naming: columns are found by their
/// header ("Timestamp", "Time", "Target", "Latency (ms)", "Result", …). Values are read the way people
/// and spreadsheets write them: ISO or local date-times, an optional "UTC offset" column, true/false
/// or Reply/Lost, and latencies with a decimal point or comma. Shared by the CSV and Excel readers.
/// </summary>
public sealed partial class PingRowReader
{
    private const string UnknownHost = "unknown host";

    private static readonly string[] TimestampNames = ["timestamp", "time", "datetime", "date", "timelocal", "localtime", "when"];
    private static readonly string[] HostNames = ["targethost", "target", "host", "address", "destination"];
    private static readonly string[] SuccessNames = ["issuccess", "success", "result", "outcome", "status"];
    private static readonly string[] RoundtripNames = ["roundtriptimemilliseconds", "roundtripms", "roundtrip", "latencyms", "latency", "rttms", "rtt", "timems"];
    private static readonly string[] DetailsNames = ["details", "detail", "message", "info", "notes"];
    private static readonly string[] OffsetNames = ["utcoffset", "offset", "timezone"];

    private readonly int timestamp;
    private readonly int host;
    private readonly int success;
    private readonly int roundtrip;
    private readonly int details;
    private readonly int offset;

    private PingRowReader(int timestamp, int host, int success, int roundtrip, int details, int offset)
    {
        this.timestamp = timestamp;
        this.host = host;
        this.success = success;
        this.roundtrip = roundtrip;
        this.details = details;
        this.offset = offset;
    }

    /// <summary>Null when the header has no time column, or neither a result nor a latency column.</summary>
    public static PingRowReader? FromHeader(IReadOnlyList<string?> header)
    {
        ArgumentNullException.ThrowIfNull(header);
        var names = header.Select(Normalize).ToList();
        int Find(string[] candidates) => candidates.Select(candidate => names.IndexOf(candidate)).FirstOrDefault(index => index >= 0, -1);

        var reader = new PingRowReader(
            Find(TimestampNames), Find(HostNames), Find(SuccessNames), Find(RoundtripNames), Find(DetailsNames), Find(OffsetNames));
        return reader.timestamp < 0 || (reader.success < 0 && reader.roundtrip < 0) ? null : reader;
    }

    /// <returns>The ping, or null with <paramref name="problem"/> saying what was wrong.</returns>
    public PingAttempt? Read(IReadOnlyList<string?> cells, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(cells);
        string Cell(int index) => index >= 0 && index < cells.Count ? (cells[index] ?? string.Empty).Trim() : string.Empty;

        problem = null;
        if (!TryTimestamp(Cell(timestamp), Cell(offset), out var when))
        {
            problem = $"unreadable time \"{Shorten(Cell(timestamp))}\"";
            return null;
        }

        long? latency = null;
        var latencyText = Cell(roundtrip);
        if (!TryLatency(latencyText, out latency))
        {
            problem = $"unreadable latency \"{Shorten(latencyText)}\"";
            return null;
        }

        bool replied;
        var resultText = Cell(success);
        if (resultText.Length == 0)
        {
            replied = latency is not null;
        }
        else if (!TryResult(resultText, out replied))
        {
            problem = $"unreadable result \"{Shorten(resultText)}\"";
            return null;
        }

        var target = Cell(host);
        if (target.Length > PingRunSettings.MaximumHostLength)
        {
            problem = "target name is too long";
            return null;
        }

        var text = Cell(details);
        return new PingAttempt(
            target.Length == 0 ? UnknownHost : target,
            when,
            replied,
            replied ? latency : null,
            text.Length == 0 ? replied ? "Reply" : "Lost" : text.ReplaceLineEndings(" "));
    }

    private static string Normalize(string? name) =>
        string.Concat((name ?? string.Empty).Where(char.IsLetterOrDigit)).ToLowerInvariant();

    private static string Shorten(string text) => text.Length <= 40 ? text : text[..40] + "…";

    private static bool TryTimestamp(string text, string offsetText, out DateTimeOffset value)
    {
        value = default;
        if (text.Length == 0)
        {
            return false;
        }

        if (ExplicitOffset().IsMatch(text))
        {
            return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out value);
        }

        if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var local)
            && !DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out local))
        {
            return false;
        }

        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        var zone = TryOffset(offsetText, out var parsed) ? parsed : TimeZoneInfo.Local.GetUtcOffset(local);
        value = new DateTimeOffset(local, zone);
        return true;
    }

    private static bool TryOffset(string text, out TimeSpan offset)
    {
        offset = default;
        var match = OffsetPattern().Match(text);
        if (!match.Success)
        {
            return false;
        }

        var hours = int.Parse(match.Groups["hours"].Value, CultureInfo.InvariantCulture);
        var minutes = match.Groups["minutes"].Success ? int.Parse(match.Groups["minutes"].Value, CultureInfo.InvariantCulture) : 0;
        offset = new TimeSpan(hours, minutes, 0) * (match.Groups["sign"].Value == "-" ? -1 : 1);
        return offset.Duration() <= TimeSpan.FromHours(14);
    }

    private static bool TryLatency(string text, out long? latency)
    {
        latency = null;
        var trimmed = text.EndsWith("ms", StringComparison.OrdinalIgnoreCase) ? text[..^2].Trim() : text;
        if (trimmed.Length == 0 || trimmed is "-" or "–")
        {
            return true;
        }

        // A decimal comma ("17,6") is read the same in every locale; there are no thousands in a latency.
        var normalized = trimmed.Contains(',', StringComparison.Ordinal) && !trimmed.Contains('.', StringComparison.Ordinal)
            ? trimmed.Replace(',', '.')
            : trimmed;
        if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return false;
        }

        if (number < 0 || !double.IsFinite(number) || number > TimeSpan.FromMinutes(10).TotalMilliseconds)
        {
            return false;
        }

        latency = (long)Math.Round(number, MidpointRounding.AwayFromZero);
        return true;
    }

    private static bool TryResult(string text, out bool replied)
    {
        switch (Normalize(text))
        {
            case "true" or "1" or "yes" or "y" or "reply" or "replied" or "success" or "succeeded" or "ok":
                replied = true;
                return true;
            case "false" or "0" or "no" or "n" or "lost" or "failed" or "fail" or "failure" or "timeout" or "timedout" or "error":
                replied = false;
                return true;
            default:
                replied = false;
                return false;
        }
    }

    [GeneratedRegex(@"(Z|[+-]\d{2}:?\d{2})\s*$")]
    private static partial Regex ExplicitOffset();

    [GeneratedRegex(@"(?<sign>[+-])(?<hours>\d{1,2})(:?(?<minutes>\d{2}))?")]
    private static partial Regex OffsetPattern();
}
