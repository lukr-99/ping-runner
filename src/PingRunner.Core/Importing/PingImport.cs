using PingRunner.Core.Pinging;

namespace PingRunner.Core.Importing;

/// <summary>
/// What a file gave up: its readable pings, ordered by time, and the rows that were left out (the first
/// <see cref="MaximumListedIssues"/> of them are listed; <see cref="SkippedRows"/> counts them all).
/// </summary>
public sealed record PingImport(string SourceName, IReadOnlyList<PingAttempt> Attempts, int SkippedRows, IReadOnlyList<ImportIssue> Issues)
{
    public const int MaximumListedIssues = 50;

    public IReadOnlyList<string> Targets =>
        [.. Attempts.Select(attempt => attempt.TargetHost).Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>The attempts split per target, each in time order, for storing one run per target.</summary>
    public IReadOnlyList<IReadOnlyList<PingAttempt>> ByTarget() =>
    [
        .. Attempts
            .GroupBy(attempt => attempt.TargetHost, StringComparer.OrdinalIgnoreCase)
            .Select(group => (IReadOnlyList<PingAttempt>)[.. group]),
    ];

    /// <summary>"Read 3,600 pings; left out 2 rows (line 17: unreadable time …)."</summary>
    public string Describe()
    {
        var read = $"Read {Attempts.Count:N0} {(Attempts.Count == 1 ? "ping" : "pings")} from {SourceName}";
        if (SkippedRows == 0)
        {
            return read + ".";
        }

        var first = Issues.Count > 0 ? $" ({Issues[0]}{(SkippedRows > 1 ? ", …" : string.Empty)})" : string.Empty;
        return $"{read}; left out {SkippedRows:N0} unreadable {(SkippedRows == 1 ? "row" : "rows")}{first}.";
    }
}
