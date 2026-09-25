using System.Globalization;
using System.Text;
using PingRunner.Core.Formatting;
using PingRunner.Core.Pinging;
using PingRunner.Core.Statistics;

namespace PingRunner.App.Formatting;

/// <summary>A plain-text report of a session, for pasting into a support ticket or a chat.</summary>
public static class SummaryText
{
    public static string Build(string version, string target, PingRunSettings? settings, PingStatistics statistics)
    {
        ArgumentNullException.ThrowIfNull(statistics);
        var culture = CultureInfo.CurrentCulture;
        var text = new StringBuilder();
        text.AppendLine(culture, $"Ping Runner {version} · {target}");

        if (statistics.FirstTimestamp is { } first && statistics.LastTimestamp is { } last)
        {
            text.Append(culture, $"{first.ToLocalTime():yyyy-MM-dd HH:mm:ss} to {last.ToLocalTime():HH:mm:ss} ({Units.Span(statistics.Span)})");
            if (settings is not null)
            {
                text.Append(culture, $", every {settings.Interval.TotalMilliseconds:0} ms, timeout {settings.Timeout.TotalMilliseconds:0} ms");
            }

            text.AppendLine();
        }

        text.AppendLine(culture, $"Sent {Units.Count(statistics.Sent)} · received {Units.Count(statistics.Received)} · lost {Units.Count(statistics.Lost)} ({Units.Percent(statistics.LossFraction)})");
        if (statistics.Latency is { } latency)
        {
            text.AppendLine(culture, $"Latency: min {Units.Milliseconds(latency.Minimum)} · avg {Units.Milliseconds(latency.Mean)} · median {Units.Milliseconds(latency.Median)} · p95 {Units.Milliseconds(latency.Percentile(95))} · p99 {Units.Milliseconds(latency.Percentile(99))} · max {Units.Milliseconds(latency.Maximum)}");
        }

        text.Append(culture, $"Jitter {Units.Milliseconds(statistics.JitterMilliseconds)} · outages {statistics.Outages.Count}");
        if (statistics.LongestOutage is { } longest)
        {
            text.Append(culture, $" (longest {Units.Span(longest.Duration)})");
        }

        if (statistics.CallQuality is { } quality)
        {
            text.Append(culture, $" · call quality {quality.MeanOpinionScore:0.0} ({quality.Rating})");
        }

        return text.AppendLine().ToString();
    }
}
