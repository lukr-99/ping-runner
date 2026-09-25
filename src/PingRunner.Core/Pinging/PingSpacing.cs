namespace PingRunner.Core.Pinging;

/// <summary>How far apart pings were, for runs whose interval is not known (imported files).</summary>
public static class PingSpacing
{
    /// <summary>The median gap between consecutive pings in time order; null with fewer than two.</summary>
    public static TimeSpan? Typical(IReadOnlyList<PingAttempt> attempts)
    {
        ArgumentNullException.ThrowIfNull(attempts);
        if (attempts.Count < 2)
        {
            return null;
        }

        var ordered = attempts.Select(attempt => attempt.Timestamp).Order().ToList();
        var gaps = ordered.Zip(ordered.Skip(1), (earlier, later) => (later - earlier).Ticks).Order().ToList();
        return TimeSpan.FromTicks(gaps[gaps.Count / 2]);
    }
}
