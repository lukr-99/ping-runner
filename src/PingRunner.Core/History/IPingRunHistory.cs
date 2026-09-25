using PingRunner.Core.Pinging;

namespace PingRunner.Core.History;

/// <summary>
/// Stored ping runs and every attempt in them. A run is started, filled in batches while it goes, then
/// finished with its summary. Failures throw <see cref="HistoryException"/>.
/// </summary>
public interface IPingRunHistory
{
    /// <returns>The new run's id.</returns>
    Task<long> StartRunAsync(PingRunSettings settings, DateTimeOffset startedAt, CancellationToken cancellationToken);

    /// <summary>Adds attempts and brings the run's sent and received counts up to date.</summary>
    /// <param name="firstSequence">The position of the first attempt in the run, counting from 0.</param>
    Task AppendAttemptsAsync(long runId, int firstSequence, IReadOnlyList<PingAttempt> attempts, CancellationToken cancellationToken);

    Task FinishRunAsync(long runId, RunOutcome outcome, DateTimeOffset? endedAt, RunSummary summary, CancellationToken cancellationToken);

    /// <summary>
    /// Stores pings read from a file as one finished run, all at once. The attempts are one target's,
    /// in time order; the interval is taken from their spacing.
    /// </summary>
    /// <returns>The new run's id.</returns>
    Task<long> ImportRunAsync(string sourceName, IReadOnlyList<PingAttempt> attempts, RunSummary summary, CancellationToken cancellationToken);

    /// <summary>Every run, newest first.</summary>
    Task<IReadOnlyList<RunRecord>> ListRunsAsync(CancellationToken cancellationToken);

    /// <summary>A run's attempts in the order they were sent.</summary>
    Task<IReadOnlyList<PingAttempt>> LoadAttemptsAsync(long runId, CancellationToken cancellationToken);

    Task DeleteRunAsync(long runId, CancellationToken cancellationToken);
}
