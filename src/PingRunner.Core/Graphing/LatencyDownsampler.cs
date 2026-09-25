using PingRunner.Core.Pinging;

namespace PingRunner.Core.Graphing;

/// <summary>
/// Thins a long series to about as many points as the graph has room for without hiding what matters:
/// each bucket keeps its first and last attempt, its fastest and slowest reply, and every failure.
/// </summary>
public static class LatencyDownsampler
{
    public static IReadOnlyList<PingAttempt> Downsample(IReadOnlyList<PingAttempt> attempts, int pointBudget)
    {
        ArgumentNullException.ThrowIfNull(attempts);
        if (pointBudget <= 0 || attempts.Count <= 2 || attempts.Count <= pointBudget)
        {
            return attempts;
        }

        var bucketSize = (int)Math.Ceiling((double)attempts.Count / pointBudget);
        var kept = new List<PingAttempt>(pointBudget * 2);

        for (var start = 0; start < attempts.Count; start += bucketSize)
        {
            var end = Math.Min(start + bucketSize, attempts.Count) - 1;
            int? fastest = null;
            int? slowest = null;

            for (var index = start; index <= end; index++)
            {
                if (attempts[index].RoundtripMilliseconds is not { } roundtrip)
                {
                    continue;
                }

                if (fastest is null || roundtrip < attempts[fastest.Value].RoundtripMilliseconds)
                {
                    fastest = index;
                }

                if (slowest is null || roundtrip > attempts[slowest.Value].RoundtripMilliseconds)
                {
                    slowest = index;
                }
            }

            for (var index = start; index <= end; index++)
            {
                if (index == start || index == end || index == fastest || index == slowest || !attempts[index].IsSuccess)
                {
                    kept.Add(attempts[index]);
                }
            }
        }

        return kept;
    }
}
