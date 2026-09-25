using Microsoft.Extensions.Time.Testing;
using PingRunner.Core.Tests.Fakes;
using PingRunner.Core.Throughput;

namespace PingRunner.Core.Tests.Throughput;

public sealed class ThroughputTestTests
{
    private static readonly ThroughputTestOptions Options = new()
    {
        Duration = TimeSpan.FromSeconds(2),
        Streams = 2,
        SampleInterval = TimeSpan.FromMilliseconds(200),
        WarmUp = TimeSpan.FromSeconds(1),
        PeakWindow = TimeSpan.FromSeconds(1),
    };

    [Fact]
    public async Task RunAsync_SteadyTransfer_ReportsTheRateAndTotal()
    {
        var time = new FakeTimeProvider();
        var endpoint = new ManualThroughputEndpoint();
        var progress = new SynchronousProgress<ThroughputProgress>();

        var run = new ThroughputTest(endpoint, time).RunAsync(ThroughputDirection.Download, Options, progress, CancellationToken.None);
        endpoint.WaitForStreams(2);
        for (var step = 0; step < 10; step++)
        {
            // Two streams of 125 kB every 200 ms: 1.25 MB/s, or 10 Mbit/s.
            endpoint.Move(125_000);
            time.Advance(Options.SampleInterval);
        }

        var result = await run;

        Assert.Equal(ThroughputDirection.Download, result.Direction);
        Assert.Equal(2_500_000, result.TotalBytes);
        Assert.Equal(10_000_000, result.AverageBitsPerSecond, 3);
        Assert.Equal(10_000_000, result.PeakBitsPerSecond, 3);
        Assert.Equal(TimeSpan.FromSeconds(2), result.Duration);
        Assert.Equal(10, progress.Reports.Count);
        Assert.Equal(10_000_000, progress.Reports[^1].CurrentBitsPerSecond, 3);
    }

    [Fact]
    public async Task RunAsync_Cancelled_Throws()
    {
        var time = new FakeTimeProvider();
        var endpoint = new ManualThroughputEndpoint();
        using var cancellation = new CancellationTokenSource();

        var run = new ThroughputTest(endpoint, time).RunAsync(ThroughputDirection.Upload, Options, null, cancellation.Token);
        endpoint.WaitForStreams(2);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }
}
