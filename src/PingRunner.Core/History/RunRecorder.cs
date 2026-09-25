using PingRunner.Core.Pinging;
using PingRunner.Core.Sessions;
using PingRunner.Core.Statistics;

namespace PingRunner.Core.History;

/// <summary>
/// Keeps every run of a <see cref="PingSession"/> in the history as it happens: the run is stored when
/// it starts, its attempts are written every <see cref="BatchSize"/> pings or
/// <see cref="FlushInterval"/>, whichever comes first, and it is finished with its summary when it
/// ends. A crash therefore loses at most the last few seconds, and the next start marks such a run as
/// interrupted (<see cref="RecoverInterruptedRuns"/>). Store work runs in order on the thread pool; the
/// events are raised on the context that created the recorder.
/// </summary>
public sealed class RunRecorder : IDisposable
{
    public const int BatchSize = 50;
    public static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(5);

    private readonly PingSession session;
    private readonly IPingRunHistory history;
    private readonly TimeProvider time;
    private readonly SynchronizationContext? context = SynchronizationContext.Current;
    private readonly object gate = new();
    private Task tail = Task.CompletedTask;
    private Recording? current;

    public RunRecorder(PingSession session, IPingRunHistory history, TimeProvider time)
    {
        this.session = session;
        this.history = history;
        this.time = time;
        session.StateChanged += OnStateChanged;
        session.AttemptRecorded += OnAttemptRecorded;
    }

    /// <summary>After a run has been finished in the store, or interrupted runs were closed.</summary>
    public event EventHandler? RunSaved;

    /// <summary>When the store refused something; the recorder keeps going with the next batch.</summary>
    public event EventHandler<HistoryException>? Failed;

    /// <summary>Finishes runs an earlier crash left marked as running, with the attempts that reached the store.</summary>
    public void RecoverInterruptedRuns() => Enqueue(async () =>
    {
        var recovered = false;
        foreach (var run in (await history.ListRunsAsync(CancellationToken.None).ConfigureAwait(false)).Where(run => run.Outcome == RunOutcome.Running))
        {
            var attempts = await history.LoadAttemptsAsync(run.Id, CancellationToken.None).ConfigureAwait(false);
            var endedAt = attempts.Count > 0 ? attempts[^1].Timestamp : run.StartedAt;
            await history.FinishRunAsync(run.Id, RunOutcome.Interrupted, endedAt, RunSummary.From(PingStatistics.From(attempts)), CancellationToken.None)
                .ConfigureAwait(false);
            recovered = true;
        }

        if (recovered)
        {
            Raise(RunSaved, EventArgs.Empty);
        }
    });

    /// <summary>Completes when everything queued so far has reached the store.</summary>
    public Task WhenIdleAsync()
    {
        lock (gate)
        {
            return tail;
        }
    }

    /// <summary>
    /// For closing the app: saves the run in progress as stopped and waits up to
    /// <paramref name="timeout"/> for the store to catch up.
    /// </summary>
    /// <returns>False when the store did not finish in time.</returns>
    public bool Close(TimeSpan timeout)
    {
        Dispose();
        if (current is { } recording)
        {
            current = null;
            Finish(recording, RunOutcome.Stopped);
        }

        return WhenIdleAsync().Wait(timeout);
    }

    public void Dispose()
    {
        session.StateChanged -= OnStateChanged;
        session.AttemptRecorded -= OnAttemptRecorded;
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (session.IsRunning && current is null && session.Settings is { } settings && session.StartedAt is { } startedAt)
        {
            var recording = new Recording(time.GetTimestamp());
            current = recording;
            Enqueue(async () => recording.Id = await history.StartRunAsync(settings, startedAt, CancellationToken.None).ConfigureAwait(false));
        }
        else if (!session.IsRunning && current is { } finished)
        {
            current = null;
            Finish(finished, session.LastRunCompleted == true ? RunOutcome.Completed : RunOutcome.Stopped);
        }
    }

    private void OnAttemptRecorded(object? sender, PingAttempt attempt)
    {
        if (current is not { } recording)
        {
            return;
        }

        recording.Pending.Add(attempt);
        recording.LastTimestamp = attempt.Timestamp;
        if (recording.Pending.Count >= BatchSize || time.GetElapsedTime(recording.LastFlush) >= FlushInterval)
        {
            Flush(recording);
        }
    }

    private void Flush(Recording recording)
    {
        if (recording.Pending.Count == 0)
        {
            return;
        }

        PingAttempt[] batch = [.. recording.Pending];
        recording.Pending.Clear();
        var firstSequence = recording.NextSequence;
        recording.NextSequence += batch.Length;
        recording.LastFlush = time.GetTimestamp();
        Enqueue(async () =>
        {
            if (recording.Id is { } id)
            {
                await history.AppendAttemptsAsync(id, firstSequence, batch, CancellationToken.None).ConfigureAwait(false);
            }
        });
    }

    private void Finish(Recording recording, RunOutcome outcome)
    {
        Flush(recording);
        var endedAt = recording.LastTimestamp;
        Enqueue(async () =>
        {
            if (recording.Id is not { } id)
            {
                return;
            }

            var attempts = await history.LoadAttemptsAsync(id, CancellationToken.None).ConfigureAwait(false);
            await history.FinishRunAsync(id, outcome, endedAt, RunSummary.From(PingStatistics.From(attempts)), CancellationToken.None)
                .ConfigureAwait(false);
            Raise(RunSaved, EventArgs.Empty);
        });
    }

    private void Enqueue(Func<Task> work)
    {
        lock (gate)
        {
            tail = tail.ContinueWith(
                async _ =>
                {
                    try
                    {
                        await work().ConfigureAwait(false);
                    }
                    catch (HistoryException exception)
                    {
                        Raise(Failed, exception);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default).Unwrap();
        }
    }

    private void Raise<T>(EventHandler<T>? handler, T args)
    {
        if (handler is null)
        {
            return;
        }

        if (context is null)
        {
            handler(this, args);
        }
        else
        {
            context.Post(_ => handler(this, args), null);
        }
    }

    private void Raise(EventHandler? handler, EventArgs args)
    {
        if (handler is null)
        {
            return;
        }

        if (context is null)
        {
            handler(this, args);
        }
        else
        {
            context.Post(_ => handler(this, args), null);
        }
    }

    // One run being recorded. Id arrives from the store; the rest is touched only on the session's thread.
    private sealed class Recording(long startedTimestamp)
    {
        public long? Id { get; set; }

        public int NextSequence { get; set; }

        public List<PingAttempt> Pending { get; } = [];

        public long LastFlush { get; set; } = startedTimestamp;

        public DateTimeOffset? LastTimestamp { get; set; }
    }
}
