namespace PingRunner.Core.History;

/// <summary>One stored ping run: what was pinged, how, when, how it ended, and its figures.</summary>
public sealed record RunRecord(
    long Id,
    string TargetHost,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    TimeSpan Interval,
    TimeSpan Timeout,
    TimeSpan? PlannedDuration,
    RunOutcome Outcome,
    RunSummary Summary)
{
    public RunSource Source { get; init; } = RunSource.Recorded;

    /// <summary>The file an imported run was read from; null for a recorded run.</summary>
    public string? SourceName { get; init; }

    public TimeSpan? Duration => EndedAt is { } ended ? ended - StartedAt : null;
}
