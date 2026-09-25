using Microsoft.Extensions.Time.Testing;
using PingRunner.Core.Pinging;
using PingRunner.Core.SpeedTest;
using PingRunner.Core.Tests.Fakes;
using PingRunner.Core.Throughput;

namespace PingRunner.Core.Tests.SpeedTest;

public sealed class SpeedTestRunnerTests
{
    private static readonly SpeedTestOptions Options = new()
    {
        IdlePings = 4,
        PingInterval = TimeSpan.FromMilliseconds(100),
        Throughput = new ThroughputTestOptions { Duration = TimeSpan.FromSeconds(1), Streams = 2, WarmUp = TimeSpan.Zero },
    };

    [Fact]
    public async Task RunAsync_MeasuresIdleLatencyThenBothDirections()
    {
        var time = new FakeTimeProvider();
        var pinger = new ScriptedPingSender(PingOutcome.Reply(20, "1.1.1.1"));
        var phases = new SynchronousProgress<SpeedTestProgress>();

        var result = await Drive(new SpeedTestRunner(new ClockedThroughputEndpoint(time, 1_000), pinger, time).RunAsync(Options, phases, CancellationToken.None), time);

        Assert.Equal("Clocked", result.Server);
        Assert.Equal(4, result.IdleLatency!.Count);
        Assert.Equal(ThroughputDirection.Download, result.Download.Direction);
        Assert.Equal(ThroughputDirection.Upload, result.Upload.Direction);
        Assert.True(result.Download.TotalBytes > 0);
        Assert.True(result.Upload.TotalBytes > 0);
        Assert.NotNull(result.DownloadLatency);
        Assert.Equal(BufferbloatGrade.APlus, result.Bufferbloat!.Grade);
        Assert.Equal(
            [SpeedTestPhase.IdleLatency, SpeedTestPhase.Download, SpeedTestPhase.Upload],
            phases.Reports.Select(report => report.Phase).Distinct());
        Assert.All(pinger.Calls, call => Assert.Equal("1.1.1.1", call.Host));
    }

    [Fact]
    public async Task RunAsync_LatencyHostSilent_StopsIdlePingsEarlyAndLeavesBufferbloatUnknown()
    {
        var time = new FakeTimeProvider();
        var pinger = new ScriptedPingSender(PingOutcome.Failure("Request timed out"));

        var result = await Drive(new SpeedTestRunner(new ClockedThroughputEndpoint(time, 1_000), pinger, time).RunAsync(Options with { IdlePings = 10 }, null, CancellationToken.None), time);

        Assert.Null(result.IdleLatency);
        Assert.Null(result.Bufferbloat);
    }

    // Moves the fake clock in small steps until the run ends; each step lets timers due by then fire.
    private static async Task<SpeedTestResult> Drive(Task<SpeedTestResult> run, FakeTimeProvider time)
    {
        for (var step = 0; step < 20_000 && !run.IsCompleted; step++)
        {
            time.Advance(TimeSpan.FromMilliseconds(10));
            await Task.Yield();
        }

        return await run;
    }
}
