using PingRunner.Core.Pinging;

namespace PingRunner.Core.Statistics;

/// <summary>
/// What a series of pings says about a connection: how many replies came back, how fast and how
/// steadily, and when it went down. Built from attempts in the order they were sent.
/// </summary>
public sealed class PingStatistics
{
    /// <summary>Lost pings in a row that count as an outage; one lost ping is only packet loss.</summary>
    public const int MinimumLostInARow = 2;

    private PingStatistics(
        int sent,
        int received,
        LatencyDistribution? latency,
        double? jitterMilliseconds,
        IReadOnlyList<Outage> outages,
        DateTimeOffset? firstTimestamp,
        DateTimeOffset? lastTimestamp)
    {
        Sent = sent;
        Received = received;
        Latency = latency;
        JitterMilliseconds = jitterMilliseconds;
        Outages = outages;
        FirstTimestamp = firstTimestamp;
        LastTimestamp = lastTimestamp;
    }

    public static PingStatistics Empty { get; } = new(0, 0, null, null, [], null, null);

    public int Sent { get; }

    public int Received { get; }

    public int Lost => Sent - Received;

    /// <summary>Lost as a share of sent, 0 to 1; null before the first ping.</summary>
    public double? LossFraction => Sent == 0 ? null : (double)Lost / Sent;

    /// <summary>Round-trip times of the replies; null when nothing came back.</summary>
    public LatencyDistribution? Latency { get; }

    /// <summary>
    /// The mean change in round-trip time between consecutive replies (lost pings in between are
    /// skipped); null with fewer than two replies.
    /// </summary>
    public double? JitterMilliseconds { get; }

    public IReadOnlyList<Outage> Outages { get; }

    public Outage? LongestOutage => Outages.MaxBy(outage => outage.Duration);

    public DateTimeOffset? FirstTimestamp { get; }

    public DateTimeOffset? LastTimestamp { get; }

    public TimeSpan Span => FirstTimestamp is { } first && LastTimestamp is { } last ? last - first : TimeSpan.Zero;

    /// <summary>Null until there are replies to judge by.</summary>
    public CallQuality? CallQuality => Latency is null || LossFraction is not { } loss
        ? null
        : Statistics.CallQuality.Estimate(Latency.Mean, JitterMilliseconds ?? 0, loss);

    public static PingStatistics From(IReadOnlyList<PingAttempt> attempts)
    {
        ArgumentNullException.ThrowIfNull(attempts);
        if (attempts.Count == 0)
        {
            return Empty;
        }

        var latencies = new List<double>(attempts.Count);
        var jitterTotal = 0d;
        var jitterPairs = 0;
        double? previousReply = null;
        var outages = new List<Outage>();
        var lostRun = new List<PingAttempt>();

        foreach (var attempt in attempts)
        {
            if (attempt is { IsSuccess: true, RoundtripMilliseconds: { } roundtrip })
            {
                latencies.Add(roundtrip);
                if (previousReply is { } previous)
                {
                    jitterTotal += Math.Abs(roundtrip - previous);
                    jitterPairs++;
                }

                previousReply = roundtrip;
                CloseOutage(lostRun, attempt.Timestamp, outages);
                continue;
            }

            if (!attempt.IsSuccess)
            {
                lostRun.Add(attempt);
            }
        }

        if (lostRun.Count >= MinimumLostInARow)
        {
            outages.Add(new Outage(lostRun[0].Timestamp, lostRun[^1].Timestamp, lostRun.Count, IsOngoing: true));
        }

        return new PingStatistics(
            attempts.Count,
            attempts.Count(attempt => attempt.IsSuccess),
            LatencyDistribution.From(latencies),
            jitterPairs == 0 ? null : jitterTotal / jitterPairs,
            outages,
            attempts[0].Timestamp,
            attempts[^1].Timestamp);
    }

    private static void CloseOutage(List<PingAttempt> lostRun, DateTimeOffset replyAt, List<Outage> outages)
    {
        if (lostRun.Count >= MinimumLostInARow)
        {
            outages.Add(new Outage(lostRun[0].Timestamp, replyAt, lostRun.Count, IsOngoing: false));
        }

        lostRun.Clear();
    }
}
