using PingRunner.Core.Statistics;

namespace PingRunner.Core.Tests.Statistics;

public sealed class LatencyDistributionTests
{
    [Fact]
    public void From_NoSamples_IsNull()
    {
        Assert.Null(LatencyDistribution.From([]));
    }

    [Fact]
    public void From_Samples_ReportsTheSpread()
    {
        var distribution = LatencyDistribution.From([40, 10, 30, 20])!;

        Assert.Equal(4, distribution.Count);
        Assert.Equal(10, distribution.Minimum);
        Assert.Equal(40, distribution.Maximum);
        Assert.Equal(25, distribution.Mean);
        Assert.Equal(25, distribution.Median);
        Assert.Equal(Math.Sqrt(125), distribution.StandardDeviation, 6);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(100, 100)]
    [InlineData(95, 95.5)]
    [InlineData(50, 55)]
    public void Percentile_InterpolatesBetweenRanks(double percent, double expected)
    {
        var distribution = LatencyDistribution.From(Enumerable.Range(1, 10).Select(value => value * 10d))!;

        Assert.Equal(expected, distribution.Percentile(percent), 6);
    }

    [Fact]
    public void Percentile_OutOfRange_Throws()
    {
        var distribution = LatencyDistribution.From([1])!;

        Assert.Throws<ArgumentOutOfRangeException>(() => distribution.Percentile(101));
    }

    [Fact]
    public void MeanOfHighest_MoreThanAvailable_UsesEverySample()
    {
        var distribution = LatencyDistribution.From([10, 20, 90])!;

        Assert.Equal(55, distribution.MeanOfHighest(2));
        Assert.Equal(40, distribution.MeanOfHighest(50));
    }
}
