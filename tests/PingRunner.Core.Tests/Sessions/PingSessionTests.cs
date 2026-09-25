using Microsoft.Extensions.Time.Testing;
using PingRunner.Core.Pinging;
using PingRunner.Core.Sessions;
using PingRunner.Core.Tests.Fakes;

namespace PingRunner.Core.Tests.Sessions;

public sealed class PingSessionTests
{
    [Fact]
    public async Task RunAsync_FixedDuration_StoresEveryAttemptAndCompletes()
    {
        var (session, time) = Session(capacity: 100);
        var recorded = 0;
        session.AttemptRecorded += (_, _) => recorded++;

        var run = session.RunAsync(Settings(TimeSpan.FromSeconds(3)));
        Assert.True(session.IsRunning);
        var completed = await Drive(run, time);

        Assert.True(completed);
        Assert.True(session.LastRunCompleted);
        Assert.False(session.IsRunning);
        Assert.Equal(3, session.Attempts.Count);
        Assert.Equal(3, recorded);
    }

    [Fact]
    public async Task RunAsync_OverCapacity_DropsTheOldest()
    {
        var (session, time) = Session(capacity: 2);

        await Drive(session.RunAsync(Settings(TimeSpan.FromSeconds(5))), time);

        Assert.Equal(2, session.Attempts.Count);
        Assert.Equal(session.StartedAt!.Value.AddSeconds(3), session.Attempts[0].Timestamp);
    }

    [Fact]
    public async Task Stop_EndsTheRunAsStopped()
    {
        var (session, _) = Session(capacity: 100);

        var run = session.RunAsync(Settings(null));
        session.Stop();

        Assert.False(await run);
        Assert.False(session.LastRunCompleted);
        Assert.False(session.IsRunning);
    }

    [Fact]
    public async Task RunAsync_WhileRunning_Throws()
    {
        var (session, _) = Session(capacity: 100);
        var first = session.RunAsync(Settings(null));

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.RunAsync(Settings(null)));

        session.Stop();
        await first;
    }

    [Fact]
    public async Task Clear_BetweenRuns_ForgetsTheAttempts()
    {
        var (session, time) = Session(capacity: 100);
        await Drive(session.RunAsync(Settings(TimeSpan.FromSeconds(2))), time);

        session.Clear();

        Assert.Empty(session.Attempts);
        Assert.Null(session.StartedAt);
    }

    private static (PingSession Session, FakeTimeProvider Time) Session(int capacity)
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 25, 8, 0, 0, TimeSpan.Zero));
        var loop = new PingLoop(new ScriptedPingSender(PingOutcome.Reply(10, "8.8.8.8")), time);
        return (new PingSession(loop, time) { Capacity = capacity }, time);
    }

    private static PingRunSettings Settings(TimeSpan? duration) =>
        PingRunSettings.TryCreate("8.8.8.8", 1000, 1000, duration, out _)!;

    private static async Task<bool> Drive(Task<bool> run, FakeTimeProvider time)
    {
        for (var step = 0; step < 1_000 && !run.IsCompleted; step++)
        {
            time.Advance(TimeSpan.FromMilliseconds(100));
            await Task.Yield();
        }

        return await run;
    }
}
