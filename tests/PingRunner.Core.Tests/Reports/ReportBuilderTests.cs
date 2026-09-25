using System.Globalization;
using PingRunner.Core.Pinging;
using PingRunner.Core.Reports;
using PingRunner.Core.SpeedTest;
using PingRunner.Core.Statistics;
using PingRunner.Core.Tests.Fakes;
using PingRunner.Core.Throughput;

namespace PingRunner.Core.Tests.Reports;

public sealed class ReportBuilderTests : IDisposable
{
    private readonly CultureInfo previous = CultureInfo.CurrentCulture;

    public ReportBuilderTests() => CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

    public void Dispose() => CultureInfo.CurrentCulture = previous;

    [Fact]
    public void Build_NoPings_Refuses()
    {
        Assert.Throws<InvalidOperationException>(() => ReportBuilder.Build(Input([])));
    }

    [Theory]
    [InlineData(10, 1)]
    [InlineData(47, 1)]
    [InlineData(48, 5)]
    [InlineData(120, 5)]
    [InlineData(600, 15)]
    [InlineData(1_440, 60)]
    [InlineData(10_000, 360)]
    [InlineData(100_000, 1_440)]
    public void BucketSizeFor_KeepsTheBreakdownShort(int minutes, int expectedMinutes)
    {
        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), ReportBuilder.BucketSizeFor(TimeSpan.FromMinutes(minutes)));
    }

    [Fact]
    public void Build_SortsPingsAndTakesThePeriodFromThem()
    {
        var report = ReportBuilder.Build(Input([Attempts.At(5, 20), Attempts.At(0, 10), Attempts.At(2, null)]));

        Assert.Equal(Attempts.Start, report.From);
        Assert.Equal(Attempts.Start.AddSeconds(5), report.To);
        Assert.Equal([0, 2, 5], report.Attempts.Select(attempt => (int)(attempt.Timestamp - Attempts.Start).TotalSeconds));
        Assert.Equal(3, report.Statistics.Sent);
        Assert.Equal("8.8.8.8", report.Target);
        Assert.Equal("Fri 25 Sep 2026, 12:00 – 12:00 (UTC+02:00)", report.PeriodText);
    }

    [Fact]
    public void Build_SlicesTheMinuteClockAligned_InTheMeasuredOffset()
    {
        // 12:00:00 to 12:02:59 at UTC+02:00, one ping every 10 seconds, the second minute lost entirely.
        var attempts = Enumerable.Range(0, 18)
            .Select(index => Attempts.At(index * 10, index is >= 6 and < 12 ? null : 20))
            .ToList();

        var report = ReportBuilder.Build(Input(attempts));

        Assert.Equal(TimeSpan.FromMinutes(1), report.BucketSize);
        Assert.Equal(3, report.Buckets.Count);
        Assert.Equal(Attempts.Start.AddMinutes(1), report.Buckets[1].Start);
        Assert.Equal(TimeSpan.FromHours(2), report.Buckets[1].Start.Offset);
        Assert.Equal(6, report.Buckets[1].Lost);
        Assert.Equal(1, report.Buckets[1].OutagesStarted);
        Assert.Null(report.Buckets[1].MeanMilliseconds);
        Assert.Equal(20, report.Buckets[0].MeanMilliseconds);
        Assert.Equal(TimeSpan.FromSeconds(10), report.TypicalInterval);
    }

    [Fact]
    public void Build_Findings_StateTheEvidence()
    {
        var attempts = Enumerable.Range(0, 18)
            .Select(index => Attempts.At(index * 10, index is >= 6 and < 12 ? null : 20))
            .ToList();

        var findings = ReportBuilder.Build(Input(attempts)).Findings;

        Assert.Equal("18 pings to 8.8.8.8 over 2 min 50 s, about one every 10.0 s.", findings[0]);
        Assert.Equal("33.3 % of pings were lost (6 of 18). A healthy wired connection usually loses under 1 %.", findings[1]);
        Assert.Equal(
            "The target stopped answering 1 time, for 1 min 00 s in all. The longest outage lasted 1 min 00 s, from 12:01:00 to 12:02:00.",
            findings[2]);
        Assert.Contains("Median latency was 20.0 ms; 95 % of replies came within 20.0 ms and the slowest took 20.0 ms.", findings);
        Assert.Contains("The worst 1 minute was 12:01–12:02, with 100.0 % of pings lost.", findings);
    }

    [Fact]
    public void Build_CleanConnection_SaysSo()
    {
        var findings = ReportBuilder.Build(Input(Attempts.Series(10, 12, 14, 12))).Findings;

        Assert.Contains("No pings were lost.", findings);
        Assert.Contains("There were no outages (2 or more lost pings in a row).", findings);
        Assert.DoesNotContain(findings, finding => finding.StartsWith("The worst", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_KeepsOnlySpeedTestsInsideThePeriod()
    {
        var inside = SpeedTest(Attempts.Start.AddSeconds(30), download: 200e6);
        var before = SpeedTest(Attempts.Start.AddMinutes(-5), download: 50e6);
        var after = SpeedTest(Attempts.Start.AddHours(1), download: 50e6);
        var attempts = Enumerable.Range(0, 7).Select(index => Attempts.At(index * 10, 15)).ToList();

        var report = ReportBuilder.Build(Input(attempts, [after, inside, before]));

        Assert.Equal([inside], report.SpeedTests);
        Assert.Contains("1 speed test in this period averaged 200 Mbps down and 20.0 Mbps up (best 220 Mbps down, 25.0 Mbps up).", report.Findings);
        Assert.Contains("Under load, latency rose by 30.0 ms (bufferbloat grade B).", report.Findings);
    }

    [Fact]
    public void Build_Options_LeaveOutWhatWasNotAskedFor()
    {
        var attempts = Enumerable.Range(0, 7).Select(index => Attempts.At(index * 10, 15)).ToList();
        var options = new ReportOptions { Title = "  ", Notes = " Ticket 4411 ", IncludeSpeedTests = false, IncludeConnection = false };

        var report = ReportBuilder.Build(Input(attempts, [SpeedTest(Attempts.Start.AddSeconds(30), 100e6)], options, publicIp: "203.0.113.9"));

        Assert.Equal(ReportOptions.DefaultTitle, report.Title);
        Assert.Equal("Ticket 4411", report.Notes);
        Assert.Empty(report.SpeedTests);
        Assert.Null(report.Connection);
        Assert.Null(report.PublicIp);
    }

    [Fact]
    public void Build_PublicIp_OnlyWhenAskedFor()
    {
        var options = new ReportOptions { IncludePublicIp = true };

        var report = ReportBuilder.Build(Input(Attempts.Series(10, 12), options: options, publicIp: "203.0.113.9"));

        Assert.Equal("203.0.113.9", report.PublicIp);
    }

    [Fact]
    public void Build_SeveralTargets_NamesThemAll()
    {
        var report = ReportBuilder.Build(Input([Attempts.At(0, 10), Attempts.At(1, 12, "1.1.1.1"), Attempts.At(2, 11)]));

        Assert.Equal("8.8.8.8, 1.1.1.1", report.Target);
    }

    [Fact]
    public void ReportText_WritesPeriodsAndSlices()
    {
        var from = new DateTimeOffset(2026, 9, 24, 22, 15, 0, TimeSpan.FromHours(-5));
        var bucket = new ReportBucket(from, from.AddMinutes(15), 1, 1, 10, 10, 10, 0);

        Assert.Equal("Thu 24 Sep 2026, 22:15 – Fri 25 Sep 2026, 01:00 (UTC-05:00)", ReportText.Period(from, from.AddHours(2.75)));
        Assert.Equal("UTC", ReportText.Offset(TimeSpan.Zero));
        Assert.Equal("22:15–22:30", ReportText.Bucket(bucket, withDate: false));
        Assert.Equal("Thu 24 Sep, 22:15–22:30", ReportText.Bucket(bucket, withDate: true));
        Assert.Equal("15 minutes", ReportText.BucketSize(TimeSpan.FromMinutes(15)));
        Assert.Equal("1 hour", ReportText.BucketSize(TimeSpan.FromHours(1)));
        Assert.Equal("1 day", ReportText.BucketSize(TimeSpan.FromDays(1)));
    }

    private static ReportInput Input(
        IReadOnlyList<PingAttempt> attempts,
        IReadOnlyList<SpeedTestResult>? speedTests = null,
        ReportOptions? options = null,
        string? publicIp = null) => new(
        "Current session",
        attempts,
        speedTests ?? [],
        null,
        publicIp,
        options ?? new ReportOptions(),
        Attempts.Start.AddHours(1),
        "2.2.0");

    private static SpeedTestResult SpeedTest(DateTimeOffset startedAt, double download) => new(
        startedAt,
        "Test",
        "1.1.1.1",
        LatencyDistribution.From([10]),
        new ThroughputResult(ThroughputDirection.Download, download, download * 1.1, 1_000, TimeSpan.FromSeconds(10), []),
        LatencyDistribution.From([40]),
        new ThroughputResult(ThroughputDirection.Upload, 20e6, 25e6, 500, TimeSpan.FromSeconds(10), []),
        LatencyDistribution.From([20]));
}
