using PingRunner.Core.History;
using PingRunner.Core.Statistics;

namespace PingRunner.Infrastructure.Tests.History;

public sealed class SqlitePingRunHistoryTests
{
    [Fact]
    public async Task Run_StartedFilledAndFinished_ReadsBackWhole()
    {
        using var history = new HistoryFixture();
        var token = TestContext.Current.CancellationToken;
        var attempts = HistoryFixture.Attempts(12);
        var summary = RunSummary.From(PingStatistics.From(attempts));

        var id = await history.Runs.StartRunAsync(HistoryFixture.Settings(duration: TimeSpan.FromMinutes(5)), HistoryFixture.Start, token);
        await history.Runs.AppendAttemptsAsync(id, 0, attempts[..5], token);
        await history.Runs.AppendAttemptsAsync(id, 5, attempts[5..], token);
        await history.Runs.FinishRunAsync(id, RunOutcome.Completed, attempts[^1].Timestamp, summary, token);

        var run = Assert.Single(await history.Runs.ListRunsAsync(token));
        Assert.Equal("8.8.8.8", run.TargetHost);
        Assert.Equal(HistoryFixture.Start, run.StartedAt);
        Assert.Equal(TimeSpan.FromHours(2), run.StartedAt.Offset);
        Assert.Equal(TimeSpan.FromSeconds(11), run.Duration);
        Assert.Equal(TimeSpan.FromMinutes(5), run.PlannedDuration);
        Assert.Equal(RunOutcome.Completed, run.Outcome);
        Assert.Equal(summary, run.Summary);
        Assert.Equal(attempts, await history.Runs.LoadAttemptsAsync(id, token));
    }

    [Fact]
    public async Task AppendAttempts_WhileRunning_KeepsTheCountsCurrent()
    {
        using var history = new HistoryFixture();
        var token = TestContext.Current.CancellationToken;
        var attempts = HistoryFixture.Attempts(10);
        var id = await history.Runs.StartRunAsync(HistoryFixture.Settings(), HistoryFixture.Start, token);

        await history.Runs.AppendAttemptsAsync(id, 0, attempts, token);

        var run = Assert.Single(await history.Runs.ListRunsAsync(token));
        Assert.Equal(RunOutcome.Running, run.Outcome);
        Assert.Equal(10, run.Summary.Sent);
        Assert.Equal(attempts.Count(attempt => attempt.IsSuccess), run.Summary.Received);
    }

    [Fact]
    public async Task AppendAttempts_SameBatchTwice_KeepsOneCopy()
    {
        using var history = new HistoryFixture();
        var token = TestContext.Current.CancellationToken;
        var attempts = HistoryFixture.Attempts(3);
        var id = await history.Runs.StartRunAsync(HistoryFixture.Settings(), HistoryFixture.Start, token);

        await history.Runs.AppendAttemptsAsync(id, 0, attempts, token);
        await history.Runs.AppendAttemptsAsync(id, 0, attempts, token);

        Assert.Equal(3, (await history.Runs.LoadAttemptsAsync(id, token)).Count);
    }

    [Fact]
    public async Task NewRun_IsListedAsRunningFirst()
    {
        using var history = new HistoryFixture();
        var token = TestContext.Current.CancellationToken;
        await history.Runs.StartRunAsync(HistoryFixture.Settings("1.1.1.1"), HistoryFixture.Start, token);
        await history.Runs.StartRunAsync(HistoryFixture.Settings("9.9.9.9"), HistoryFixture.Start.AddHours(1), token);

        var runs = await history.Runs.ListRunsAsync(token);

        Assert.Equal(["9.9.9.9", "1.1.1.1"], runs.Select(run => run.TargetHost));
        Assert.All(runs, run => Assert.Equal(RunOutcome.Running, run.Outcome));
        Assert.Null(runs[0].EndedAt);
    }

    [Fact]
    public async Task DeleteRun_RemovesItsAttemptsToo()
    {
        using var history = new HistoryFixture();
        var token = TestContext.Current.CancellationToken;
        var id = await history.Runs.StartRunAsync(HistoryFixture.Settings(), HistoryFixture.Start, token);
        await history.Runs.AppendAttemptsAsync(id, 0, HistoryFixture.Attempts(4), token);

        await history.Runs.DeleteRunAsync(id, token);

        var info = await history.Database.GetInfoAsync(token);
        Assert.Equal(0, info.Runs);
        Assert.Equal(0, info.Attempts);
    }
}
