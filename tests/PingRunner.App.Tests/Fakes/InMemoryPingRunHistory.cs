using PingRunner.Core.History;
using PingRunner.Core.Pinging;

namespace PingRunner.App.Tests.Fakes;

/// <summary>Stores runs in memory; can be told to refuse writes like a full disk would.</summary>
public sealed class InMemoryPingRunHistory : IPingRunHistory
{
    private readonly object gate = new();
    private readonly Dictionary<long, RunRecord> runs = [];
    private readonly Dictionary<long, SortedDictionary<int, PingAttempt>> attempts = [];
    private long nextId = 1;

    public bool RefuseWrites { get; set; }

    public RunRecord Run(long id)
    {
        lock (gate)
        {
            return runs[id];
        }
    }

    public int StoredAttempts(long id)
    {
        lock (gate)
        {
            return attempts[id].Count;
        }
    }

    public Task<long> StartRunAsync(PingRunSettings settings, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        Check();
        lock (gate)
        {
            var id = nextId++;
            runs[id] = new RunRecord(id, settings.TargetHost, startedAt, null, settings.Interval, settings.Timeout, settings.Duration, RunOutcome.Running, RunSummary.Empty);
            attempts[id] = [];
            return Task.FromResult(id);
        }
    }

    public Task AppendAttemptsAsync(long runId, int firstSequence, IReadOnlyList<PingAttempt> batch, CancellationToken cancellationToken)
    {
        Check();
        lock (gate)
        {
            for (var index = 0; index < batch.Count; index++)
            {
                attempts[runId].TryAdd(firstSequence + index, batch[index]);
            }

            var stored = attempts[runId].Values;
            runs[runId] = runs[runId] with
            {
                Summary = runs[runId].Summary with { Sent = stored.Count, Received = stored.Count(attempt => attempt.IsSuccess) },
            };
        }

        return Task.CompletedTask;
    }

    public Task FinishRunAsync(long runId, RunOutcome outcome, DateTimeOffset? endedAt, RunSummary summary, CancellationToken cancellationToken)
    {
        Check();
        lock (gate)
        {
            runs[runId] = runs[runId] with { Outcome = outcome, EndedAt = endedAt, Summary = summary };
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RunRecord>> ListRunsAsync(CancellationToken cancellationToken)
    {
        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<RunRecord>>([.. runs.Values.OrderByDescending(run => run.StartedAt)]);
        }
    }

    public Task<IReadOnlyList<PingAttempt>> LoadAttemptsAsync(long runId, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<PingAttempt>>([.. attempts[runId].Values]);
        }
    }

    public Task DeleteRunAsync(long runId, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            runs.Remove(runId);
            attempts.Remove(runId);
        }

        return Task.CompletedTask;
    }

    private void Check()
    {
        if (RefuseWrites)
        {
            throw new HistoryException("The disk is full.");
        }
    }
}
