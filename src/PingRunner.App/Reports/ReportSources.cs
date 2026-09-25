using System.Net.Http;
using PingRunner.App.ViewModels;
using PingRunner.Core.History;
using PingRunner.Core.Network;
using PingRunner.Core.Pinging;
using PingRunner.Core.Sessions;
using PingRunner.Core.SpeedTest;

namespace PingRunner.App.Reports;

/// <summary>
/// Where a report's material comes from: the live session, what the Graph shows, the stored runs and
/// speed tests, and the connection as it is now. Failures of the history throw
/// <see cref="HistoryException"/>; the public address is simply left out when no service answers.
/// </summary>
public sealed class ReportSources(
    PingSession session,
    GraphViewModel graph,
    IPingRunHistory runs,
    ISpeedTestHistory speedTests,
    IConnectionInfoSource connections,
    IPublicIpSource publicIp)
{
    public IReadOnlyList<PingAttempt> LiveSession() => session.Snapshot();

    public bool GraphShowsOtherData => graph.IsImported;

    public string GraphLabel => graph.SourceText;

    public IReadOnlyList<PingAttempt> GraphAttempts() => graph.SourceAttempts();

    public Task<IReadOnlyList<RunRecord>> ListRunsAsync(CancellationToken cancellationToken) => runs.ListRunsAsync(cancellationToken);

    public Task<IReadOnlyList<PingAttempt>> LoadRunAsync(long runId, CancellationToken cancellationToken) => runs.LoadAttemptsAsync(runId, cancellationToken);

    /// <summary>Every stored ping to <paramref name="target"/> from <paramref name="from"/> up to, not including, <paramref name="to"/>.</summary>
    public async Task<IReadOnlyList<PingAttempt>> LoadPeriodAsync(
        IReadOnlyList<RunRecord> stored,
        string target,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stored);
        var attempts = new List<PingAttempt>();
        foreach (var run in stored.Where(run =>
            string.Equals(run.TargetHost, target, StringComparison.OrdinalIgnoreCase)
            && run.StartedAt < to
            && (run.EndedAt is not { } ended || ended >= from)))
        {
            var loaded = await runs.LoadAttemptsAsync(run.Id, cancellationToken).ConfigureAwait(true);
            attempts.AddRange(loaded.Where(attempt => attempt.Timestamp >= from && attempt.Timestamp < to));
        }

        return [.. attempts.OrderBy(attempt => attempt.Timestamp)];
    }

    public async Task<IReadOnlyList<SpeedTestResult>> SpeedTestsAsync(CancellationToken cancellationToken) =>
        [.. (await speedTests.ListAsync(cancellationToken).ConfigureAwait(true)).Select(record => record.Result)];

    public ConnectionSnapshot? Connection() => connections.GetPrimary();

    public async Task<string?> PublicIpAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await publicIp.GetAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
}
