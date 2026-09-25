using PingRunner.App.Formatting;
using PingRunner.Core.Statistics;

namespace PingRunner.App.ViewModels;

/// <summary>
/// A <see cref="PingStatistics"/> written out for the stat tiles, with a severity for the figures
/// that decide whether a connection is healthy: loss above 1 % warns and above 5 % is bad; call
/// quality follows its rating.
/// </summary>
public sealed record StatisticsDisplay
{
    public static StatisticsDisplay Empty { get; } = From(PingStatistics.Empty);

    public required string Average { get; init; }

    public required string AverageHint { get; init; }

    public required string Median { get; init; }

    public required string Percentiles { get; init; }

    public required string Jitter { get; init; }

    public required string JitterHint { get; init; }

    public required string Loss { get; init; }

    public required string LossHint { get; init; }

    public required Severity LossSeverity { get; init; }

    public required string Quality { get; init; }

    public required string QualityHint { get; init; }

    public required Severity QualitySeverity { get; init; }

    public required string Sent { get; init; }

    public required string SentHint { get; init; }

    public required string Outages { get; init; }

    public required string OutagesHint { get; init; }

    public required Severity OutageSeverity { get; init; }

    public required string Spikes { get; init; }

    public static StatisticsDisplay From(PingStatistics statistics)
    {
        ArgumentNullException.ThrowIfNull(statistics);
        var latency = statistics.Latency;
        var quality = statistics.CallQuality;
        var longest = statistics.LongestOutage;

        return new StatisticsDisplay
        {
            Average = Units.Milliseconds(latency?.Mean),
            AverageHint = latency is null ? "No replies yet" : $"min {Units.Milliseconds(latency.Minimum)} · max {Units.Milliseconds(latency.Maximum)}",
            Median = Units.Milliseconds(latency?.Median),
            Percentiles = latency is null ? "p95 / p99" : $"p95 {Units.Milliseconds(latency.Percentile(95))} · p99 {Units.Milliseconds(latency.Percentile(99))}",
            Jitter = Units.Milliseconds(statistics.JitterMilliseconds),
            JitterHint = latency is null ? "Change between replies" : $"σ {Units.Milliseconds(latency.StandardDeviation)}",
            Loss = Units.Percent(statistics.LossFraction),
            LossHint = statistics.Sent == 0 ? "Lost pings" : $"{Units.Count(statistics.Lost)} of {Units.Count(statistics.Sent)} lost",
            LossSeverity = statistics.LossFraction switch
            {
                null => Severity.Neutral,
                0 => Severity.Good,
                <= 0.01 => Severity.Neutral,
                <= 0.05 => Severity.Warning,
                _ => Severity.Bad,
            },
            Quality = quality is null ? Units.None : $"{quality.MeanOpinionScore:0.0} · {quality.Rating}",
            QualityHint = "Estimated MOS for voice",
            QualitySeverity = quality?.Rating switch
            {
                null => Severity.Neutral,
                CallQualityRating.Excellent or CallQualityRating.Good => Severity.Good,
                CallQualityRating.Fair => Severity.Neutral,
                CallQualityRating.Poor => Severity.Warning,
                _ => Severity.Bad,
            },
            Sent = Units.Count(statistics.Sent),
            SentHint = statistics.Sent == 0 ? "Pings sent" : $"{Units.Count(statistics.Received)} replies over {Units.Span(statistics.Span)}",
            Outages = Units.Count(statistics.Outages.Count),
            OutagesHint = longest is null
                ? $"{PingStatistics.MinimumLostInARow}+ lost in a row"
                : $"longest {Units.Span(longest.Duration)}{(longest.IsOngoing ? ", ongoing" : string.Empty)}",
            OutageSeverity = longest switch
            {
                null => statistics.Sent == 0 ? Severity.Neutral : Severity.Good,
                { IsOngoing: true } => Severity.Bad,
                _ => Severity.Warning,
            },
            Spikes = latency is null ? Units.None : Units.Milliseconds(latency.MeanOfHighest(50)),
        };
    }
}
