namespace PingRunner.Core.Statistics;

/// <summary>
/// A run of lost pings long enough to count as the connection being down (see
/// <see cref="PingStatistics.MinimumLostInARow"/>). It starts at the first lost ping and ends at the
/// next reply; while it is still going on, it ends at the latest lost ping.
/// </summary>
public sealed record Outage(DateTimeOffset Start, DateTimeOffset End, int LostCount, bool IsOngoing)
{
    public TimeSpan Duration => End - Start;
}
