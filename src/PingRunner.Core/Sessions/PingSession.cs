using PingRunner.Core.Pinging;

namespace PingRunner.Core.Sessions;

/// <summary>
/// The live session: runs one ping loop at a time and keeps its newest attempts, up to
/// <see cref="Capacity"/>, oldest dropping off first. Events are raised on the context that called
/// <see cref="RunAsync"/>, so a UI that starts the run on its own thread gets them there.
/// </summary>
public sealed class PingSession(PingLoop loop, TimeProvider time)
{
    public const int DefaultCapacity = 50_000;

    private readonly List<PingAttempt> attempts = [];
    private CancellationTokenSource? running;
    private int capacity = DefaultCapacity;

    /// <summary>After each attempt is stored.</summary>
    public event EventHandler<PingAttempt>? AttemptRecorded;

    /// <summary>When a run starts or ends, or the attempts are cleared.</summary>
    public event EventHandler? StateChanged;

    public int Capacity
    {
        get => capacity;
        set
        {
            capacity = Math.Max(1, value);
            Trim();
        }
    }

    public IReadOnlyList<PingAttempt> Attempts => attempts;

    public PingRunSettings? Settings { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public bool IsRunning => running is not null;

    public bool IsStopping => running?.IsCancellationRequested == true;

    /// <summary>Whether the last run went its full length (true) or was stopped (false); null before any run.</summary>
    public bool? LastRunCompleted { get; private set; }

    /// <summary>A copy of the stored attempts, safe to keep while the session goes on.</summary>
    public PingAttempt[] Snapshot() => [.. attempts];

    /// <summary>Starts a new run, replacing the stored attempts, and finishes when it ends.</summary>
    /// <returns>True when it ran its full duration, false when it was stopped.</returns>
    public async Task<bool> RunAsync(PingRunSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (running is not null)
        {
            throw new InvalidOperationException("A ping run is already in progress.");
        }

        attempts.Clear();
        Settings = settings;
        LastRunCompleted = null;
        StartedAt = time.GetLocalNow();
        running = new CancellationTokenSource();
        StateChanged?.Invoke(this, EventArgs.Empty);

        var token = running.Token;
        try
        {
            await foreach (var attempt in loop.RunAsync(settings, token))
            {
                attempts.Add(attempt);
                Trim();
                AttemptRecorded?.Invoke(this, attempt);
            }

            return !token.IsCancellationRequested;
        }
        finally
        {
            LastRunCompleted = !token.IsCancellationRequested;
            running.Dispose();
            running = null;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Stop()
    {
        if (running is { IsCancellationRequested: false })
        {
            running.Cancel();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Forgets the stored attempts; only between runs.</summary>
    public void Clear()
    {
        if (running is not null)
        {
            return;
        }

        attempts.Clear();
        Settings = null;
        StartedAt = null;
        LastRunCompleted = null;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Trim()
    {
        if (attempts.Count > capacity)
        {
            attempts.RemoveRange(0, attempts.Count - capacity);
        }
    }
}
