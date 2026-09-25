using PingRunner.Core.SpeedTest;
using PingRunner.Core.Statistics;
using PingRunner.Core.Throughput;

namespace PingRunner.Core.Tests.SpeedTest;

public sealed class SpeedTestResultTests
{
    private static readonly ThroughputResult Download = new(ThroughputDirection.Download, 100e6, 120e6, 1_000, TimeSpan.FromSeconds(10), []);
    private static readonly ThroughputResult Upload = new(ThroughputDirection.Upload, 20e6, 25e6, 500, TimeSpan.FromSeconds(10), []);

    [Fact]
    public void Bufferbloat_UsesTheWorseLoadedMedian()
    {
        var result = Result(idle: [10, 12, 14], download: [40, 42, 44], upload: [90, 95, 100]);

        Assert.Equal(83, result.Bufferbloat!.IncreaseMilliseconds);
        Assert.Equal(BufferbloatGrade.C, result.Bufferbloat.Grade);
        Assert.Equal(1_500, result.TotalBytes);
    }

    [Fact]
    public void Bufferbloat_NoIdleLatency_IsUnknown()
    {
        Assert.Null(Result(idle: [], download: [40], upload: [40]).Bufferbloat);
    }

    [Fact]
    public void Bufferbloat_LoadedFasterThanIdle_IsZero()
    {
        Assert.Equal(0, Result(idle: [30], download: [20], upload: []).Bufferbloat!.IncreaseMilliseconds);
    }

    private static SpeedTestResult Result(double[] idle, double[] download, double[] upload) => new(
        DateTimeOffset.UnixEpoch,
        "Test",
        "1.1.1.1",
        LatencyDistribution.From(idle),
        Download,
        LatencyDistribution.From(download),
        Upload,
        LatencyDistribution.From(upload));
}
