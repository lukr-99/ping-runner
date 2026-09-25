using PingRunner.Core.History;
using PingRunner.Core.Pinging;
using PingRunner.Core.SpeedTest;

namespace PingRunner.Infrastructure.History;

/// <summary>
/// Stands in when the history cannot be opened (a file from a newer Ping Runner): the app runs, lists
/// stay empty, nothing is written over the file, and <see cref="Problem"/> says why.
/// </summary>
public sealed class UnavailableHistory(string location, string problem) : IPingRunHistory, ISpeedTestHistory, IHistoryMaintenance
{
    public string Location { get; } = location;

    public string? Problem { get; } = problem;

    public Task<long> StartRunAsync(PingRunSettings settings, DateTimeOffset startedAt, CancellationToken cancellationToken) => Task.FromResult(0L);

    public Task AppendAttemptsAsync(long runId, int firstSequence, IReadOnlyList<PingAttempt> attempts, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task FinishRunAsync(long runId, RunOutcome outcome, DateTimeOffset? endedAt, RunSummary summary, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyList<RunRecord>> ListRunsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<RunRecord>>([]);

    public Task<IReadOnlyList<PingAttempt>> LoadAttemptsAsync(long runId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PingAttempt>>([]);

    public Task DeleteRunAsync(long runId, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<long> SaveAsync(SpeedTestResult result, CancellationToken cancellationToken) => Task.FromResult(0L);

    public Task<IReadOnlyList<SpeedTestRecord>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<SpeedTestRecord>>([]);

    public Task DeleteAsync(long id, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<HistoryStorageInfo> GetInfoAsync(CancellationToken cancellationToken) => Task.FromResult(HistoryStorageInfo.Empty);

    public Task BackupAsync(string destinationPath, CancellationToken cancellationToken) => Task.FromException(new HistoryException(Problem ?? "The history is unavailable."));

    public Task<HistoryStorageInfo> RestoreAsync(string sourcePath, CancellationToken cancellationToken) =>
        Task.FromException<HistoryStorageInfo>(new HistoryException(Problem ?? "The history is unavailable."));

    public Task ClearAsync(CancellationToken cancellationToken) => Task.FromException(new HistoryException(Problem ?? "The history is unavailable."));
}
