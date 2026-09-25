using PingRunner.Core.Statistics;

namespace PingRunner.Core.History;

/// <summary>
/// The headline figures of a finished run, stored beside it so the history list does not have to load
/// every attempt. Derived from the attempts; the attempts stay the source of truth.
/// </summary>
public sealed record RunSummary(
    int Sent,
    int Received,
    double? MeanMilliseconds,
    double? MedianMilliseconds,
    double? P95Milliseconds,
    double? MinimumMilliseconds,
    double? MaximumMilliseconds,
    double? JitterMilliseconds,
    int Outages,
    TimeSpan? LongestOutage,
    double? MeanOpinionScore)
{
    public static RunSummary Empty { get; } = From(PingStatistics.Empty);

    public double? LossFraction => Sent == 0 ? null : (double)(Sent - Received) / Sent;

    public static RunSummary From(PingStatistics statistics)
    {
        ArgumentNullException.ThrowIfNull(statistics);
        var latency = statistics.Latency;
        return new RunSummary(
            statistics.Sent,
            statistics.Received,
            latency?.Mean,
            latency?.Median,
            latency?.Percentile(95),
            latency?.Minimum,
            latency?.Maximum,
            statistics.JitterMilliseconds,
            statistics.Outages.Count,
            statistics.LongestOutage?.Duration,
            statistics.CallQuality?.MeanOpinionScore);
    }
}
