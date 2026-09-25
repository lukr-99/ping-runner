using PingRunner.Core.Formatting;
using PingRunner.Core.Pinging;
using PingRunner.Core.SpeedTest;
using PingRunner.Core.Statistics;

namespace PingRunner.Core.Reports;

/// <summary>
/// Builds a report from pings and what surrounds them: the statistics, a breakdown into at most
/// <see cref="MaximumBuckets"/> clock-aligned slices, the speed tests run inside the period, and
/// findings that state the evidence in plain words without judging beyond it.
/// </summary>
public static class ReportBuilder
{
    public const int MaximumBuckets = 48;

    private static readonly TimeSpan[] BucketSizes =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(6),
        TimeSpan.FromDays(1),
    ];

    /// <exception cref="InvalidOperationException">There are no pings to report on.</exception>
    public static ConnectionReport Build(ReportInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Attempts.Count == 0)
        {
            throw new InvalidOperationException("There are no pings to report on.");
        }

        var attempts = input.Attempts.OrderBy(attempt => attempt.Timestamp).ToList();
        var from = attempts[0].Timestamp;
        var to = attempts[^1].Timestamp;
        var statistics = PingStatistics.From(attempts);
        var bucketSize = BucketSizeFor(to - from);
        var buckets = BucketsOf(attempts, statistics, bucketSize);
        var options = input.Options;
        var speedTests = options.IncludeSpeedTests
            ? input.SpeedTests.Where(test => test.StartedAt >= from && test.StartedAt <= to).OrderBy(test => test.StartedAt).ToList()
            : [];
        var target = string.Join(", ", attempts.Select(attempt => attempt.TargetHost).Distinct(StringComparer.OrdinalIgnoreCase));
        var interval = TypicalInterval(attempts);

        return new ConnectionReport
        {
            Title = string.IsNullOrWhiteSpace(options.Title) ? ReportOptions.DefaultTitle : options.Title.Trim(),
            Notes = options.Notes.Trim(),
            Subject = input.Subject,
            Target = target,
            AppVersion = input.AppVersion,
            GeneratedAt = input.GeneratedAt,
            From = from,
            To = to,
            TypicalInterval = interval,
            Statistics = statistics,
            BucketSize = bucketSize,
            Buckets = buckets,
            Findings = Findings(target, statistics, interval, bucketSize, buckets, speedTests, to - from),
            Method = Method(target, interval, from.Offset, speedTests, input.AppVersion),
            SpeedTests = speedTests,
            Connection = options.IncludeConnection ? input.Connection : null,
            PublicIp = options.IncludePublicIp ? input.PublicIp : null,
            Attempts = attempts,
            IncludeAllPings = options.IncludeAllPings,
            AccentColor = options.AccentColor,
        };
    }

    /// <summary>The smallest clock-friendly slice that keeps the period to at most <see cref="MaximumBuckets"/> slices.</summary>
    public static TimeSpan BucketSizeFor(TimeSpan span) =>
        BucketSizes.FirstOrDefault(size => span.Ticks / size.Ticks < MaximumBuckets, BucketSizes[^1]);

    private static List<ReportBucket> BucketsOf(List<PingAttempt> attempts, PingStatistics statistics, TimeSpan size) =>
    [
        .. attempts
            .GroupBy(attempt => new DateTimeOffset(attempt.Timestamp.DateTime.Ticks / size.Ticks * size.Ticks, attempt.Timestamp.Offset))
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var slice = PingStatistics.From([.. group]);
                var end = group.Key + size;
                return new ReportBucket(
                    group.Key,
                    end,
                    slice.Sent,
                    slice.Received,
                    slice.Latency?.Mean,
                    slice.Latency?.Percentile(95),
                    slice.Latency?.Maximum,
                    statistics.Outages.Count(outage => outage.Start >= group.Key && outage.Start < end));
            }),
    ];

    private static TimeSpan? TypicalInterval(List<PingAttempt> attempts)
    {
        if (attempts.Count < 2)
        {
            return null;
        }

        var gaps = attempts.Zip(attempts.Skip(1), (earlier, later) => (later.Timestamp - earlier.Timestamp).Ticks).Order().ToList();
        return TimeSpan.FromTicks(gaps[gaps.Count / 2]);
    }

    private static List<string> Method(string target, TimeSpan? interval, TimeSpan offset, List<SpeedTestResult> speedTests, string appVersion)
    {
        var every = interval is { } gap && gap > TimeSpan.Zero ? $", about one every {Units.Span(gap)}," : string.Empty;
        var method = new List<string>
        {
            $"Ping Runner {appVersion} sent ICMP echo requests (pings) to {target}{every} and recorded whether each one was answered and how long the answer took.",
            $"Packet loss is the share of pings that got no answer. An outage is {PingStatistics.MinimumLostInARow} or more lost pings in a row; "
                + "it runs from the first lost ping to the next answer.",
            "Latency figures use answered pings only. Jitter is the average change in latency from one answer to the next. "
                + "Call quality is an estimate from the ITU-T G.107 E-model, not a measured call.",
            $"Times are in {ReportText.Offset(offset)}, the time zone the pings were recorded in.",
        };

        if (speedTests.Count > 0)
        {
            var servers = string.Join(", ", speedTests.Select(test => test.Server).Distinct(StringComparer.OrdinalIgnoreCase));
            method.Add($"Speed tests download and upload data through {servers} and ping while the line is full; the rise in latency over idle is the bufferbloat.");
        }

        return method;
    }

    private static List<string> Findings(
        string target,
        PingStatistics statistics,
        TimeSpan? interval,
        TimeSpan bucketSize,
        List<ReportBucket> buckets,
        List<SpeedTestResult> speedTests,
        TimeSpan span)
    {
        var findings = new List<string>
        {
            interval is { } every && every > TimeSpan.Zero
                ? $"{Units.CountOf(statistics.Sent, "ping", "pings")} to {target} over {Units.Span(span)}, about one every {Units.Span(every)}."
                : $"{Units.CountOf(statistics.Sent, "ping", "pings")} to {target}.",
        };

        findings.Add(statistics.Lost == 0
            ? "No pings were lost."
            : $"{Units.Percent(statistics.LossFraction)} of pings were lost ({Units.Count(statistics.Lost)} of {Units.Count(statistics.Sent)})."
                + (statistics.LossFraction > 0.01 ? " A healthy wired connection usually loses under 1 %." : string.Empty));

        var outages = statistics.Outages;
        if (outages.Count == 0)
        {
            findings.Add($"There were no outages ({PingStatistics.MinimumLostInARow} or more lost pings in a row).");
        }
        else
        {
            var longest = statistics.LongestOutage!;
            var total = TimeSpan.FromTicks(outages.Sum(outage => outage.Duration.Ticks));
            var withDate = span > TimeSpan.FromDays(1);
            var when = withDate ? longest.Start.ToString("ddd d MMM, HH:mm:ss", System.Globalization.CultureInfo.CurrentCulture) : ReportText.Time(longest.Start);
            findings.Add(
                $"The target stopped answering {Units.CountOf(outages.Count, "time", "times")}, for {Units.Span(total)} in all. "
                + $"The longest outage lasted {Units.Span(longest.Duration)}, from {when} to {ReportText.Time(longest.End)}"
                + (longest.IsOngoing ? " and was still going on when the pings end." : "."));
        }

        if (statistics.Latency is { } latency)
        {
            findings.Add(
                $"Median latency was {Units.Milliseconds(latency.Median)}; 95 % of replies came within {Units.Milliseconds(latency.Percentile(95))} "
                + $"and the slowest took {Units.Milliseconds(latency.Maximum)}.");
        }

        if (statistics.JitterMilliseconds is { } jitter)
        {
            findings.Add($"Latency changed by {Units.Milliseconds(jitter)} on average from one reply to the next (jitter).");
        }

        var worst = buckets.Where(bucket => bucket.Lost > 0).MaxBy(bucket => bucket.LossFraction);
        if (buckets.Count > 1 && worst is not null)
        {
            findings.Add(
                $"The worst {ReportText.BucketSize(bucketSize)} was {ReportText.Bucket(worst, span > TimeSpan.FromDays(1))}, "
                + $"with {Units.Percent(worst.LossFraction)} of pings lost.");
        }

        if (statistics.CallQuality is { } quality)
        {
            findings.Add($"Estimated voice call quality: {quality.MeanOpinionScore:0.0} out of 4.5 ({quality.Rating.ToString().ToLowerInvariant()}).");
        }

        if (speedTests.Count > 0)
        {
            var down = speedTests.Average(test => test.Download.AverageBitsPerSecond);
            var up = speedTests.Average(test => test.Upload.AverageBitsPerSecond);
            var tests = Units.CountOf(speedTests.Count, "speed test", "speed tests");
            findings.Add(
                $"{tests} in this period averaged {Units.BitsPerSecond(down)} down and {Units.BitsPerSecond(up)} up "
                + $"(best {Units.BitsPerSecond(speedTests.Max(test => test.Download.PeakBitsPerSecond))} down, "
                + $"{Units.BitsPerSecond(speedTests.Max(test => test.Upload.PeakBitsPerSecond))} up).");
            if (speedTests[^1].Bufferbloat is { } bloat)
            {
                findings.Add($"Under load, latency rose by {Units.Milliseconds(bloat.IncreaseMilliseconds)} (bufferbloat grade {bloat.GradeText}).");
            }
        }

        return findings;
    }
}
