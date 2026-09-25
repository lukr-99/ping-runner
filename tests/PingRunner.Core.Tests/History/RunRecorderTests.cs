using Microsoft.Extensions.Time.Testing;
using PingRunner.Core.History;
using PingRunner.Core.Pinging;
using PingRunner.Core.Sessions;
using PingRunner.Core.Tests.Fakes;

namespace PingRunner.Core.Tests.History;

public sealed class RunRecorderTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 25, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task FinishedRun_IsStoredWithEveryAttemptAndItsSummary()
    {
        var (session, time, history, recorder) = Build();
        var saved = 0;
        recorder.RunSaved += (_, _) => Interlocked.Increment(ref saved);

        await Drive(session.RunAsync(Settings(TimeSpan.FromSeconds(120))), time);
        await recorder.WhenIdleAsync();

        var run = history.Run(1);
        Assert.Equal(RunOutcome.Completed, run.Outcome);
        Assert.Equal(120, history.StoredAttempts(1));
        Assert.Equal(120, run.Summary.Sent);
        Assert.Equal(Start.AddSeconds(119), run.EndedAt);
        Assert.Equal(1, Volatile.Read(ref saved));
    }

    [Fact]
    public async Task RunningRun_ReachesTheStoreEveryFewSeconds()
    {
        var (session, time, history, recorder) = Build();

        var run = session.RunAsync(Settings(null));
        await Drive(() => session.Attempts.Count >= 12, time);
        await recorder.WhenIdleAsync();

        Assert.Equal(RunOutcome.Running, history.Run(1).Outcome);
        Assert.InRange(history.StoredAttempts(1), 5, 12);
        session.Stop();
        await run;
    }

    [Fact]
    public async Task StoppedRun_IsStoredAsStopped()
    {
        var (session, time, history, recorder) = Build();

        var run = session.RunAsync(Settings(null));
        await Drive(() => session.Attempts.Count >= 3, time);
        session.Stop();
        await run;
        await recorder.WhenIdleAsync();

        Assert.Equal(RunOutcome.Stopped, history.Run(1).Outcome);
        Assert.Equal(session.Attempts.Count, history.StoredAttempts(1));
    }

    [Fact]
    public async Task Close_WhileRunning_SavesWhatThereIsAsStopped()
    {
        var (session, time, history, recorder) = Build();
        _ = session.RunAsync(Settings(null));
        await Drive(() => session.Attempts.Count >= 7, time);

        Assert.True(recorder.Close(TimeSpan.FromSeconds(10)));

        Assert.Equal(RunOutcome.Stopped, history.Run(1).Outcome);
        Assert.Equal(session.Attempts.Count, history.StoredAttempts(1));
        session.Stop();
    }

    [Fact]
    public async Task RecoverInterruptedRuns_ClosesRunsACrashLeftOpen()
    {
        var (_, _, history, recorder) = Build();
        var settings = Settings(null);
        var id = await history.StartRunAsync(settings, Start, TestContext.Current.CancellationToken);
        await history.AppendAttemptsAsync(
            id,
            0,
            [new PingAttempt("8.8.8.8", Start, true, 10, "Reply"), new PingAttempt("8.8.8.8", Start.AddSeconds(1), false, null, "Lost")],
            TestContext.Current.CancellationToken);

        recorder.RecoverInterruptedRuns();
        await recorder.WhenIdleAsync();

        var run = history.Run(id);
        Assert.Equal(RunOutcome.Interrupted, run.Outcome);
        Assert.Equal(Start.AddSeconds(1), run.EndedAt);
        Assert.Equal(2, run.Summary.Sent);
        Assert.Equal(1, run.Summary.Received);
    }

    [Fact]
    public async Task StoreRefuses_TheFailureIsReported()
    {
        var (session, time, history, recorder) = Build();
        history.RefuseWrites = true;
        var failures = 0;
        recorder.Failed += (_, _) => Interlocked.Increment(ref failures);

        await Drive(session.RunAsync(Settings(TimeSpan.FromSeconds(3))), time);
        await recorder.WhenIdleAsync();

        Assert.True(Volatile.Read(ref failures) >= 1);
    }

    private static (PingSession Session, FakeTimeProvider Time, InMemoryPingRunHistory History, RunRecorder Recorder) Build()
    {
        var time = new FakeTimeProvider(Start);
        var session = new PingSession(new PingLoop(new ScriptedPingSender(PingOutcome.Reply(12, "8.8.8.8")), time), time);
        var history = new InMemoryPingRunHistory();
        return (session, time, history, new RunRecorder(session, history, time));
    }

    private static PingRunSettings Settings(TimeSpan? duration) =>
        PingRunSettings.TryCreate("8.8.8.8", 1000, 1000, duration, out _)!;

    private static async Task Drive(Task run, FakeTimeProvider time)
    {
        for (var step = 0; step < 10_000 && !run.IsCompleted; step++)
        {
            time.Advance(TimeSpan.FromMilliseconds(250));
            await Task.Yield();
        }

        await run;
    }

    private static async Task Drive(Func<bool> done, FakeTimeProvider time)
    {
        for (var step = 0; step < 10_000 && !done(); step++)
        {
            time.Advance(TimeSpan.FromMilliseconds(250));
            await Task.Yield();
        }
    }
}
