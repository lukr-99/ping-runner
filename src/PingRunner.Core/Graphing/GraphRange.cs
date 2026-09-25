using PingRunner.Core.Pinging;

namespace PingRunner.Core.Graphing;

/// <summary>Which part of a session the graph shows before zooming: the last N attempts, a time span, or all.</summary>
public sealed record GraphRange(string Label, GraphRangeMode Mode, int RecentAttempts = 0, TimeSpan TimeWindow = default)
{
    public static GraphRange Everything { get; } = new("All", GraphRangeMode.All);

    public static IReadOnlyList<GraphRange> Presets { get; } =
    [
        new("Last 60 pings", GraphRangeMode.RecentAttempts, RecentAttempts: 60),
        new("Last 300 pings", GraphRangeMode.RecentAttempts, RecentAttempts: 300),
        new("Last 1,000 pings", GraphRangeMode.RecentAttempts, RecentAttempts: 1_000),
        new("Last 5 minutes", GraphRangeMode.RollingTime, TimeWindow: TimeSpan.FromMinutes(5)),
        new("Last 15 minutes", GraphRangeMode.RollingTime, TimeWindow: TimeSpan.FromMinutes(15)),
        new("Last hour", GraphRangeMode.RollingTime, TimeWindow: TimeSpan.FromHours(1)),
        new("Last 24 hours", GraphRangeMode.RollingTime, TimeWindow: TimeSpan.FromHours(24)),
        Everything,
    ];

    /// <param name="attempts">In the order they were sent.</param>
    public IReadOnlyList<PingAttempt> Apply(IReadOnlyList<PingAttempt> attempts)
    {
        ArgumentNullException.ThrowIfNull(attempts);
        if (attempts.Count == 0)
        {
            return attempts;
        }

        return Mode switch
        {
            GraphRangeMode.RecentAttempts when RecentAttempts > 0 && RecentAttempts < attempts.Count =>
                [.. attempts.Skip(attempts.Count - RecentAttempts)],
            GraphRangeMode.RollingTime when TimeWindow > TimeSpan.Zero =>
                [.. attempts.Where(attempt => attempt.Timestamp >= attempts[^1].Timestamp - TimeWindow)],
            _ => attempts,
        };
    }

    public override string ToString() => Label;
}
