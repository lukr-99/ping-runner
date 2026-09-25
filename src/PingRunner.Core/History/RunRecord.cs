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
    public TimeSpan? Duration => EndedAt is { } ended ? ended - StartedAt : null;
}
