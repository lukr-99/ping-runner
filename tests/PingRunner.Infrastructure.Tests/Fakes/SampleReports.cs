using PingRunner.Core.Network;
using PingRunner.Core.Pinging;
using PingRunner.Core.Reports;
using PingRunner.Core.SpeedTest;
using PingRunner.Core.Statistics;
using PingRunner.Core.Throughput;

namespace PingRunner.Infrastructure.Tests.Fakes;

/// <summary>Reports built from made-up pings, for the writers to write.</summary>
public static class SampleReports
{
    public static readonly DateTimeOffset Start = new(2026, 9, 25, 14, 0, 0, TimeSpan.FromHours(2));

    /// <summary>A 1×1 PNG, enough for a writer to place.</summary>
    public static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    /// <summary>One ping a second for <paramref name="seconds"/>; every 97th to 99th second lost, making outages.</summary>
    public static List<PingAttempt> Attempts(int seconds, string host = "8.8.8.8") =>
    [
        .. Enumerable.Range(0, seconds).Select(second =>
        {
            var lost = second % 100 is >= 97 and <= 99;
            return new PingAttempt(
                host,
                Start.AddSeconds(second),
                !lost,
                lost ? null : 15 + (second % 7),
                lost ? "Request timed out" : $"Reply from {host}");
        }),
    ];

    public static ConnectionReport Report(int seconds = 600, ReportOptions? options = null)
    {
        var report = ReportBuilder.Build(new ReportInput(
            "Current session",
            Attempts(seconds),
            [SpeedTest(Start.AddSeconds(seconds / 2))],
            new ConnectionSnapshot("Ethernet", "Intel(R) Ethernet Controller I225-V", "Ethernet", 1_000_000_000, ["192.168.1.20"], ["192.168.1.1"], ["192.168.1.1", "1.1.1.1"]),
            "203.0.113.9",
            options ?? new ReportOptions { Notes = "Ticket 4411\nTechnician visit on Monday.", IncludePublicIp = true },
            Start.AddSeconds(seconds + 60),
            "2.2.0"));
        return report with { Charts = [new ReportChart("Latency over time", "Each point is a ping.", TinyPng, 1600, 640)] };
    }

    public static SpeedTestResult SpeedTest(DateTimeOffset startedAt) => new(
        startedAt,
        "speed.cloudflare.com",
        "1.1.1.1",
        LatencyDistribution.From([12, 13, 14]),
        new ThroughputResult(ThroughputDirection.Download, 480e6, 520e6, 600_000_000, TimeSpan.FromSeconds(10), []),
        LatencyDistribution.From([40, 42, 44]),
        new ThroughputResult(ThroughputDirection.Upload, 95e6, 110e6, 120_000_000, TimeSpan.FromSeconds(10), []),
        LatencyDistribution.From([30, 32]));
}
