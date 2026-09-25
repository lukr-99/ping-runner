using System.IO;
using System.Windows.Media.Imaging;
using PingRunner.App.Reports;
using PingRunner.App.Tests.Hosting;
using PingRunner.Core.Network;
using PingRunner.Core.Pinging;
using PingRunner.Core.Reports;
using PingRunner.Core.SpeedTest;
using PingRunner.Core.Statistics;
using PingRunner.Core.Throughput;
using PingRunner.Infrastructure.Reports;

namespace PingRunner.App.Tests.Reports;

/// <summary>
/// Draws the report charts for a realistic evening of pings. With PINGRUNNER_SCREENSHOTS set to a
/// folder it also writes the charts and the finished PDF and Excel report there, for looking at.
/// </summary>
[Collection(WpfCollection.Name)]
public sealed class ReportChartRendererTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 25, 18, 0, 0, TimeSpan.FromHours(2));

    [Fact]
    public Task Draw_AnEveningOfPings_MakesEveryChartAtTwiceTheSize() => WpfHost.RunAsync(async () =>
    {
        var report = SampleReport();

        var charts = new ReportChartRenderer().Draw(report);

        Assert.Equal(["Latency over time", "Packet loss per 5 minutes", "How replies were spread", "Speed tests"], charts.Select(chart => chart.Title));
        foreach (var chart in charts)
        {
            using var png = new MemoryStream(chart.Png);
            var frame = BitmapDecoder.Create(png, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            Assert.Equal(ReportChartRenderer.Width * 2, frame.PixelWidth);
            Assert.Equal(ReportChartRenderer.Height * 2, frame.PixelHeight);
            Assert.False(string.IsNullOrWhiteSpace(chart.Caption));
        }

        if (Environment.GetEnvironmentVariable("PINGRUNNER_SCREENSHOTS") is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder);
            foreach (var (chart, index) in charts.Select((chart, index) => (chart, index)))
            {
                await File.WriteAllBytesAsync(Path.Combine(folder, $"report-chart-{index + 1}.png"), chart.Png);
            }

            var finished = report with { Charts = charts };
            foreach (IReportWriter writer in new IReportWriter[] { new PdfReportWriter(), new ExcelReportWriter() })
            {
                var stream = File.Create(Path.Combine(folder, "sample-report" + writer.FileExtension));
                await using (stream)
                {
                    await writer.WriteAsync(finished, stream, CancellationToken.None);
                }
            }
        }
    });

    [Fact]
    public Task Draw_OnePing_StillDrawsTheTimeline() => WpfHost.RunAsync(() =>
    {
        var report = ReportBuilder.Build(new ReportInput(
            "Current session", [new PingAttempt("8.8.8.8", Start, true, 12, "Reply")], [], null, null, new ReportOptions(), Start, "2.2.0"));

        var charts = new ReportChartRenderer().Draw(report);

        Assert.Equal("Latency over time", Assert.Single(charts).Title);
        return Task.CompletedTask;
    });

    // Two hours at one ping a second: a calm start, an evening slowdown with loss, and two outages.
    private static ConnectionReport SampleReport()
    {
        var random = new Random(7);
        var attempts = new List<PingAttempt>();
        for (var second = 0; second < 7_200; second++)
        {
            var time = Start.AddSeconds(second);
            var busy = second is > 3_000 and < 5_400;
            var outage = second is >= 4_100 and < 4_145 or >= 6_000 and < 6_012;
            var lost = outage || random.NextDouble() < (busy ? 0.012 : 0.001);
            var latency = (busy ? 38 : 16) + (random.NextDouble() * (busy ? 30 : 6)) + (random.NextDouble() < 0.004 ? 400 : 0);
            attempts.Add(lost
                ? new PingAttempt("8.8.8.8", time, false, null, "Request timed out")
                : new PingAttempt("8.8.8.8", time, true, (long)latency, "Reply from 8.8.8.8"));
        }

        var speedTests = new[] { 900, 4_500, 6_600 }.Select(second => new SpeedTestResult(
            Start.AddSeconds(second),
            "Cloudflare",
            "1.1.1.1",
            LatencyDistribution.From([14, 15, 16]),
            new ThroughputResult(ThroughputDirection.Download, second == 4_500 ? 180e6 : 470e6, 510e6, 600_000_000, TimeSpan.FromSeconds(10), []),
            LatencyDistribution.From([48, 52, 60]),
            new ThroughputResult(ThroughputDirection.Upload, second == 4_500 ? 40e6 : 92e6, 105e6, 120_000_000, TimeSpan.FromSeconds(10), []),
            LatencyDistribution.From([34, 38]))).ToList();

        return ReportBuilder.Build(new ReportInput(
            "Stored run",
            attempts,
            speedTests,
            new ConnectionSnapshot("Ethernet", "Intel(R) Ethernet Controller I225-V", "Ethernet", 1_000_000_000, ["192.168.1.20"], ["192.168.1.1"], ["192.168.1.1"]),
            null,
            new ReportOptions { Title = "Evening slowdowns", Notes = "Provider ticket 4411.\nThe slowdown starts around 19:00 most days." },
            Start.AddHours(2.2),
            "2.2.0"));
    }
}
