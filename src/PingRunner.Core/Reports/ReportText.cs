using System.Globalization;
using PingRunner.Core.Formatting;

namespace PingRunner.Core.Reports;

/// <summary>How reports write times and periods: in the offset the pings were measured in, never the reader's.</summary>
public static class ReportText
{
    private static CultureInfo Culture => CultureInfo.CurrentCulture;

    public static string Offset(TimeSpan offset) =>
        offset == TimeSpan.Zero ? "UTC" : $"UTC{(offset < TimeSpan.Zero ? "-" : "+")}{offset.Duration():hh\\:mm}";

    public static string Stamp(DateTimeOffset value) => value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    public static string Time(DateTimeOffset value) => value.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    public static string Period(DateTimeOffset from, DateTimeOffset to)
    {
        var start = from.ToString("ddd d MMM yyyy, HH:mm", Culture);
        var end = from.Date == to.Date ? to.ToString("HH:mm", Culture) : to.ToString("ddd d MMM yyyy, HH:mm", Culture);
        return $"{start} – {end} ({Offset(from.Offset)})";
    }

    /// <summary>A slice of the period: "14:00–14:15", with the day when the period spans several.</summary>
    public static string Bucket(ReportBucket bucket, bool withDate) =>
        withDate
            ? $"{bucket.Start.ToString("ddd d MMM, HH:mm", Culture)}–{bucket.End.ToString("HH:mm", Culture)}"
            : $"{bucket.Start.ToString("HH:mm", Culture)}–{bucket.End.ToString("HH:mm", Culture)}";

    /// <summary>"1 minute", "15 minutes", "1 hour", "6 hours", "1 day".</summary>
    public static string BucketSize(TimeSpan size) => size switch
    {
        { TotalDays: >= 1 } => Units.CountOf((long)size.TotalDays, "day", "days"),
        { TotalHours: >= 1 } => Units.CountOf((long)size.TotalHours, "hour", "hours"),
        _ => Units.CountOf((long)size.TotalMinutes, "minute", "minutes"),
    };
}
