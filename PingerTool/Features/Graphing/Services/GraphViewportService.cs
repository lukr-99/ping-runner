using PingerTool.Core.Pinging.Models;
using PingerTool.Features.Graphing.Models;

namespace PingerTool.Features.Graphing.Services;

public static class GraphViewportService
{
    public static IReadOnlyList<PingAttemptResult> BuildViewport(
        IEnumerable<PingAttemptResult>? attempts,
        GraphRangeMode rangeMode,
        int? recentAttemptLimit,
        TimeSpan? rollingTimeWindow,
        double zoomFactor,
        double panRatio)
    {
        var filteredAttempts = ApplyRangeFilter(attempts, rangeMode, recentAttemptLimit, rollingTimeWindow);
        return ApplyZoomWindow(filteredAttempts, zoomFactor, panRatio);
    }

    public static IReadOnlyList<PingAttemptResult> Downsample(
        IReadOnlyList<PingAttemptResult> attempts,
        int targetPointBudget)
    {
        if (attempts.Count <= 2 || attempts.Count <= targetPointBudget || targetPointBudget <= 0)
        {
            return attempts;
        }

        var bucketSize = Math.Max(1, (int)Math.Ceiling((double)attempts.Count / targetPointBudget));
        var downsampledAttempts = new List<PingAttemptResult>(targetPointBudget * 2);

        for (var bucketStartIndex = 0; bucketStartIndex < attempts.Count; bucketStartIndex += bucketSize)
        {
            var bucket = attempts
                .Skip(bucketStartIndex)
                .Take(bucketSize)
                .Select((attempt, index) => new IndexedAttempt(bucketStartIndex + index, attempt))
                .ToList();

            if (bucket.Count == 0)
            {
                continue;
            }

            var selectedIndices = new HashSet<int>
            {
                bucket[0].Index,
                bucket[^1].Index,
            };

            var successfulAttempts = bucket
                .Where(item => item.Attempt.IsSuccess && item.Attempt.RoundtripTimeMilliseconds is not null)
                .ToList();

            if (successfulAttempts.Count > 0)
            {
                var minimumLatencyAttempt = successfulAttempts.MinBy(item => item.Attempt.RoundtripTimeMilliseconds) ?? successfulAttempts[0];
                var maximumLatencyAttempt = successfulAttempts.MaxBy(item => item.Attempt.RoundtripTimeMilliseconds) ?? successfulAttempts[0];
                selectedIndices.Add(minimumLatencyAttempt.Index);
                selectedIndices.Add(maximumLatencyAttempt.Index);
            }

            foreach (var failure in bucket.Where(item => !item.Attempt.IsSuccess))
            {
                selectedIndices.Add(failure.Index);
            }

            foreach (var attempt in bucket.Where(item => selectedIndices.Contains(item.Index)).OrderBy(item => item.Index))
            {
                downsampledAttempts.Add(attempt.Attempt);
            }
        }

        return downsampledAttempts;
    }

    private static IReadOnlyList<PingAttemptResult> ApplyRangeFilter(
        IEnumerable<PingAttemptResult>? attempts,
        GraphRangeMode rangeMode,
        int? recentAttemptLimit,
        TimeSpan? rollingTimeWindow)
    {
        if (attempts is null)
        {
            return [];
        }

        var orderedAttempts = attempts
            .OrderBy(attempt => attempt.Timestamp)
            .ToList();

        if (orderedAttempts.Count == 0)
        {
            return orderedAttempts;
        }

        return rangeMode switch
        {
            GraphRangeMode.RecentAttempts when recentAttemptLimit is > 0
                => orderedAttempts.TakeLast(recentAttemptLimit.Value).ToList(),
            GraphRangeMode.RollingTimeWindow when rollingTimeWindow is { } timeWindow && timeWindow > TimeSpan.Zero
                => orderedAttempts
                    .Where(attempt => attempt.Timestamp >= orderedAttempts[^1].Timestamp - timeWindow)
                    .ToList(),
            _ => orderedAttempts,
        };
    }

    private static IReadOnlyList<PingAttemptResult> ApplyZoomWindow(
        IReadOnlyList<PingAttemptResult> attempts,
        double zoomFactor,
        double panRatio)
    {
        if (attempts.Count <= 2 || zoomFactor <= 1)
        {
            return attempts;
        }

        var clampedZoomFactor = Math.Clamp(zoomFactor, 1, 12);
        var visibleAttemptCount = Math.Max(2, (int)Math.Ceiling(attempts.Count / clampedZoomFactor));

        if (visibleAttemptCount >= attempts.Count)
        {
            return attempts;
        }

        var maximumStartIndex = attempts.Count - visibleAttemptCount;
        var startIndex = (int)Math.Round(Math.Clamp(panRatio, 0, 1) * maximumStartIndex);

        return attempts
            .Skip(startIndex)
            .Take(visibleAttemptCount)
            .ToList();
    }

    private sealed record IndexedAttempt(int Index, PingAttemptResult Attempt);
}
