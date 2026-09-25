using PingRunner.Core.Graphing;
using PingRunner.Core.Tests.Fakes;

namespace PingRunner.Core.Tests.Graphing;

public sealed class LatencyDownsamplerTests
{
    [Fact]
    public void Downsample_WithinBudget_ReturnsTheSameList()
    {
        var attempts = Attempts.Series(1, 2, 3);

        Assert.Same(attempts, LatencyDownsampler.Downsample(attempts, 10));
    }

    [Fact]
    public void Downsample_KeepsSpikesDipsAndFailures()
    {
        // One bucket of ten: first, last, the 99 ms spike, the 1 ms dip and the failure survive.
        var attempts = Attempts.Series(20, 21, 99, 22, null, 23, 1, 24, 25, 26, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20);

        var kept = LatencyDownsampler.Downsample(attempts, 2);

        var firstBucket = kept.Where(attempt => attempt.Timestamp < Attempts.Start.AddSeconds(10)).ToList();
        Assert.Equal(
            [20L, 99L, null, 1L, 26L],
            firstBucket.Select(attempt => attempt.RoundtripMilliseconds));
        Assert.True(kept.Count < attempts.Count);
    }
}
