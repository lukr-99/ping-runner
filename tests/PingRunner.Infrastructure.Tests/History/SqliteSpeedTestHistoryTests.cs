using PingRunner.Core.SpeedTest;
using PingRunner.Core.Statistics;
using PingRunner.Core.Throughput;

namespace PingRunner.Infrastructure.Tests.History;

public sealed class SqliteSpeedTestHistoryTests
{
    [Fact]
    public async Task Save_ThenList_ReadsTheTestBackWhole()
    {
        using var history = new HistoryFixture();
        var token = TestContext.Current.CancellationToken;
        var result = new SpeedTestResult(
            HistoryFixture.Start,
            "Cloudflare (speed.cloudflare.com)",
            "1.1.1.1",
            LatencyDistribution.From([12, 13, 15]),
            new ThroughputResult(ThroughputDirection.Download, 312e6, 340e6, 400_000_000, TimeSpan.FromSeconds(10),
                [new(TimeSpan.FromMilliseconds(200), 280e6), new(TimeSpan.FromMilliseconds(400), 330e6)]),
            LatencyDistribution.From([40, 42]),
            new ThroughputResult(ThroughputDirection.Upload, 41e6, 44e6, 51_000_000, TimeSpan.FromSeconds(10), []),
            null);

        var id = await history.SpeedTests.SaveAsync(result, token);
        var stored = Assert.Single(await history.SpeedTests.ListAsync(token));

        Assert.Equal(id, stored.Id);
        var read = stored.Result;
        Assert.Equal(result.StartedAt, read.StartedAt);
        Assert.Equal(result.StartedAt.Offset, read.StartedAt.Offset);
        Assert.Equal(result.Server, read.Server);
        Assert.Equal(result.Download.AverageBitsPerSecond, read.Download.AverageBitsPerSecond);
        Assert.Equal(result.Download.Series, read.Download.Series);
        Assert.Equal(result.Upload.TotalBytes, read.Upload.TotalBytes);
        Assert.Equal([12d, 13d, 15d], read.IdleLatency!.Samples);
        Assert.Null(read.UploadLatency);
        Assert.Equal(result.Bufferbloat, read.Bufferbloat);
    }

    [Fact]
    public async Task Delete_RemovesTheTest()
    {
        using var history = new HistoryFixture();
        var token = TestContext.Current.CancellationToken;
        var id = await history.SpeedTests.SaveAsync(Minimal(), token);

        await history.SpeedTests.DeleteAsync(id, token);

        Assert.Empty(await history.SpeedTests.ListAsync(token));
    }

    internal static SpeedTestResult Minimal() => new(
        HistoryFixture.Start,
        "Test",
        "1.1.1.1",
        null,
        new ThroughputResult(ThroughputDirection.Download, 1e6, 1e6, 1, TimeSpan.FromSeconds(1), []),
        null,
        new ThroughputResult(ThroughputDirection.Upload, 1e6, 1e6, 1, TimeSpan.FromSeconds(1), []),
        null);
}
