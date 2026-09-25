namespace PingRunner.Core.Statistics;

/// <summary>
/// The spread of a set of latency samples in milliseconds. Percentiles interpolate linearly between
/// the two closest ranks (the same as a spreadsheet's PERCENTILE.INC), so the median of an even count
/// is the mean of the middle two. The standard deviation is the population one.
/// </summary>
public sealed class LatencyDistribution
{
    private readonly double[] sorted;

    private LatencyDistribution(double[] sorted)
    {
        this.sorted = sorted;
        Mean = sorted.Average();
        StandardDeviation = Math.Sqrt(sorted.Sum(sample => (sample - Mean) * (sample - Mean)) / sorted.Length);
    }

    public int Count => sorted.Length;

    /// <summary>Every sample, fastest first.</summary>
    public IReadOnlyList<double> Samples => sorted;

    public double Minimum => sorted[0];

    public double Maximum => sorted[^1];

    public double Mean { get; }

    public double StandardDeviation { get; }

    public double Median => Percentile(50);

    /// <summary>Null when there are no samples.</summary>
    public static LatencyDistribution? From(IEnumerable<double> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var ordered = samples.Where(double.IsFinite).Order().ToArray();
        return ordered.Length == 0 ? null : new LatencyDistribution(ordered);
    }

    /// <param name="percent">0 to 100.</param>
    public double Percentile(double percent)
    {
        if (percent is < 0 or > 100 || double.IsNaN(percent))
        {
            throw new ArgumentOutOfRangeException(nameof(percent), percent, "A percentile is between 0 and 100.");
        }

        var rank = percent / 100 * (sorted.Length - 1);
        var lower = (int)Math.Floor(rank);
        var upper = (int)Math.Ceiling(rank);
        return sorted[lower] + ((sorted[upper] - sorted[lower]) * (rank - lower));
    }

    /// <summary>The mean of the highest <paramref name="count"/> samples (the worst spikes).</summary>
    public double MeanOfHighest(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        return sorted.TakeLast(Math.Min(count, sorted.Length)).Average();
    }
}
