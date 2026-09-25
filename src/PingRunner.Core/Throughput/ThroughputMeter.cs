namespace PingRunner.Core.Throughput;

/// <summary>
/// Turns cumulative byte counts read at known times into rates. Samples must move forward in time and
/// bytes never shrink; a sample that breaks either is ignored. Rates are in bits per second.
/// </summary>
public sealed class ThroughputMeter
{
    private readonly List<ThroughputSample> samples = [new(TimeSpan.Zero, 0)];

    public IReadOnlyList<ThroughputSample> Samples => samples;

    public long TotalBytes => samples[^1].TotalBytes;

    public TimeSpan Elapsed => samples[^1].Elapsed;

    public void Record(TimeSpan elapsed, long totalBytes)
    {
        var last = samples[^1];
        if (elapsed <= last.Elapsed || totalBytes < last.TotalBytes)
        {
            return;
        }

        samples.Add(new ThroughputSample(elapsed, totalBytes));
    }

    /// <summary>The rate over the most recent <paramref name="window"/>, or since the start if shorter.</summary>
    public double CurrentBitsPerSecond(TimeSpan window)
    {
        var last = samples[^1];
        var from = samples.LastOrDefault(sample => last.Elapsed - sample.Elapsed >= window) ?? samples[0];
        return Rate(from, last);
    }

    /// <summary>The rate after the warm-up; the whole transfer's rate when it ended before the warm-up did.</summary>
    public double AverageBitsPerSecond(TimeSpan warmUp)
    {
        var last = samples[^1];
        var from = samples.FirstOrDefault(sample => sample.Elapsed >= warmUp);
        return from is null || from == last ? Rate(samples[0], last) : Rate(from, last);
    }

    /// <summary>
    /// The best rate sustained across at least <paramref name="window"/>; the whole transfer's rate
    /// when it was shorter than the window.
    /// </summary>
    public double PeakBitsPerSecond(TimeSpan window)
    {
        var peak = 0d;
        var found = false;
        var start = 0;
        for (var end = 1; end < samples.Count; end++)
        {
            while (start + 1 < end && samples[end].Elapsed - samples[start + 1].Elapsed >= window)
            {
                start++;
            }

            if (samples[end].Elapsed - samples[start].Elapsed < window)
            {
                continue;
            }

            peak = Math.Max(peak, Rate(samples[start], samples[end]));
            found = true;
        }

        return found ? peak : Rate(samples[0], samples[^1]);
    }

    public IReadOnlyList<ThroughputPoint> Series() =>
        [.. samples.Zip(samples.Skip(1), (from, to) => new ThroughputPoint(to.Elapsed, Rate(from, to)))];

    public ThroughputResult ToResult(ThroughputDirection direction, ThroughputTestOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new ThroughputResult(
            direction,
            AverageBitsPerSecond(options.WarmUp),
            PeakBitsPerSecond(options.PeakWindow),
            TotalBytes,
            Elapsed,
            Series());
    }

    private static double Rate(ThroughputSample from, ThroughputSample to)
    {
        var seconds = (to.Elapsed - from.Elapsed).TotalSeconds;
        return seconds <= 0 ? 0 : (to.TotalBytes - from.TotalBytes) * 8d / seconds;
    }
}
