namespace PingRunner.Core.Statistics;

/// <summary>
/// An estimate of how a voice call would sound over this connection, as a mean opinion score from 1
/// (bad) to 4.5 (best a narrow-band call reaches). It uses the common simplification of the ITU-T
/// G.107 E-model that network monitors use: jitter counts double against latency, 10 ms is added for
/// codec delay, and every percent of loss costs 2.5 points of R. It is a rough guide, not a
/// measurement of a real call.
/// </summary>
public sealed record CallQuality(double RFactor, double MeanOpinionScore)
{
    public CallQualityRating Rating => RatingOf(MeanOpinionScore);

    /// <summary>The band a score falls in: 4.3 and up excellent, 4.0 good, 3.6 fair, 3.1 poor, below that bad.</summary>
    public static CallQualityRating RatingOf(double meanOpinionScore) => meanOpinionScore switch
    {
        >= 4.3 => CallQualityRating.Excellent,
        >= 4.0 => CallQualityRating.Good,
        >= 3.6 => CallQualityRating.Fair,
        >= 3.1 => CallQualityRating.Poor,
        _ => CallQualityRating.Bad,
    };

    /// <param name="meanLatencyMilliseconds">Mean round-trip time.</param>
    /// <param name="jitterMilliseconds">Mean change between consecutive round trips.</param>
    /// <param name="lossFraction">0 to 1.</param>
    public static CallQuality Estimate(double meanLatencyMilliseconds, double jitterMilliseconds, double lossFraction)
    {
        var effectiveLatency = Math.Max(0, meanLatencyMilliseconds) + (Math.Max(0, jitterMilliseconds) * 2) + 10;
        var r = effectiveLatency < 160
            ? 93.2 - (effectiveLatency / 40)
            : 93.2 - ((effectiveLatency - 120) / 10);
        r -= Math.Clamp(lossFraction, 0, 1) * 100 * 2.5;
        r = Math.Clamp(r, 0, 100);

        var mos = 1 + (0.035 * r) + (0.000007 * r * (r - 60) * (100 - r));
        return new CallQuality(r, Math.Clamp(mos, 1, 4.5));
    }
}
