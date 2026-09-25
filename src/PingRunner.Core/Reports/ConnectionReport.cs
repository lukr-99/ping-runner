using PingRunner.Core.Network;
using PingRunner.Core.Pinging;
using PingRunner.Core.SpeedTest;
using PingRunner.Core.Statistics;

namespace PingRunner.Core.Reports;

/// <summary>
/// A finished report, ready for a writer: the figures, the breakdown over time, the findings in plain
/// words, the speed tests and connection details, the raw pings and the charts. Times keep the offset
/// they were measured in.
/// </summary>
public sealed record ConnectionReport
{
    public required string Title { get; init; }

    public required string Notes { get; init; }

    public required string Subject { get; init; }

    public required string Target { get; init; }

    public required string AppVersion { get; init; }

    public required DateTimeOffset GeneratedAt { get; init; }

    public required DateTimeOffset From { get; init; }

    public required DateTimeOffset To { get; init; }

    /// <summary>The usual gap between pings; null with a single ping.</summary>
    public required TimeSpan? TypicalInterval { get; init; }

    public required PingStatistics Statistics { get; init; }

    public required TimeSpan BucketSize { get; init; }

    public required IReadOnlyList<ReportBucket> Buckets { get; init; }

    public required IReadOnlyList<string> Findings { get; init; }

    /// <summary>How the figures were measured and what the terms mean, for the reader who was not there.</summary>
    public required IReadOnlyList<string> Method { get; init; }

    public required IReadOnlyList<SpeedTestResult> SpeedTests { get; init; }

    public required ConnectionSnapshot? Connection { get; init; }

    public required string? PublicIp { get; init; }

    public required IReadOnlyList<PingAttempt> Attempts { get; init; }

    public required bool IncludeAllPings { get; init; }

    public required string AccentColor { get; init; }

    public IReadOnlyList<ReportChart> Charts { get; init; } = [];

    public TimeSpan Span => To - From;

    /// <summary>"Fri 25 Sep 2026, 14:02 – 16:10 (UTC+02:00)".</summary>
    public string PeriodText => ReportText.Period(From, To);
}
