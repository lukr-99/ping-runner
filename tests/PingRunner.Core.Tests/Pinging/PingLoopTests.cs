using Microsoft.Extensions.Time.Testing;
using PingRunner.Core.Pinging;
using PingRunner.Core.Tests.Fakes;

namespace PingRunner.Core.Tests.Pinging;

public sealed class PingLoopTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 25, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RunAsync_FixedDuration_SendsOncePerIntervalThenStops()
    {
        var time = new FakeTimeProvider(Start);
        var sender = new ScriptedPingSender(PingOutcome.Reply(12, "8.8.8.8"));
        var settings = PingRunSettings.TryCreate("8.8.8.8", 1000, 1000, TimeSpan.FromSeconds(3), out _)!;

        var attempts = await CollectAsync(new PingLoop(sender, time), settings, time, TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.Equal(3, attempts.Count);
        Assert.Equal([0d, 1d, 2d], attempts.Select(attempt => (attempt.Timestamp - Start).TotalSeconds));
        Assert.All(attempts, attempt => Assert.Equal(12, attempt.RoundtripMilliseconds));
    }

    [Fact]
    public async Task RunAsync_NextPing_WaitsForTheWholeInterval()
    {
        var time = new FakeTimeProvider(Start);
        var loop = new PingLoop(new ScriptedPingSender(PingOutcome.Reply(5, "1.1.1.1")), time);
        var settings = PingRunSettings.TryCreate("1.1.1.1", 1000, 1000, null, out _)!;
        await using var attempts = loop.RunAsync(settings, TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);

        Assert.True(await attempts.MoveNextAsync());
        var second = attempts.MoveNextAsync();
        time.Advance(TimeSpan.FromMilliseconds(999));
        Assert.False(second.IsCompleted);

        time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True(await second);
        Assert.Equal(Start.AddSeconds(1), attempts.Current.Timestamp);
    }

    [Fact]
    public async Task RunAsync_FailedReply_KeepsTheDetailsAndDropsTheTime()
    {
        var time = new FakeTimeProvider(Start);
        var loop = new PingLoop(new ScriptedPingSender(PingOutcome.Failure("Request timed out")), time);
        var settings = PingRunSettings.TryCreate("10.0.0.1", 1000, 1000, null, out _)!;
        await using var attempts = loop.RunAsync(settings, TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);

        Assert.True(await attempts.MoveNextAsync());

        Assert.False(attempts.Current.IsSuccess);
        Assert.Null(attempts.Current.RoundtripMilliseconds);
        Assert.Equal("Request timed out", attempts.Current.Details);
    }

    [Fact]
    public async Task RunAsync_Cancelled_EndsWithoutThrowing()
    {
        var time = new FakeTimeProvider(Start);
        var loop = new PingLoop(new ScriptedPingSender(PingOutcome.Reply(5, "1.1.1.1")), time);
        var settings = PingRunSettings.TryCreate("1.1.1.1", 1000, 1000, null, out _)!;
        using var cancellation = new CancellationTokenSource();
        await using var attempts = loop.RunAsync(settings, cancellation.Token).GetAsyncEnumerator(TestContext.Current.CancellationToken);

        Assert.True(await attempts.MoveNextAsync());
        var next = attempts.MoveNextAsync();
        await cancellation.CancelAsync();

        Assert.False(await next);
    }

    private static async Task<List<PingAttempt>> CollectAsync(
        PingLoop loop,
        PingRunSettings settings,
        FakeTimeProvider time,
        TimeSpan step,
        CancellationToken cancellationToken)
    {
        var collected = new List<PingAttempt>();
        await using var attempts = loop.RunAsync(settings, cancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        for (var guard = 0; guard < 1_000; guard++)
        {
            var next = attempts.MoveNextAsync();
            while (!next.IsCompleted)
            {
                time.Advance(step);
            }

            if (!await next)
            {
                return collected;
            }

            collected.Add(attempts.Current);
        }

        throw new InvalidOperationException("The loop did not stop.");
    }
}
