using PingRunner.Core.Statistics;

namespace PingRunner.Core.Tests.Statistics;

public sealed class CallQualityTests
{
    [Fact]
    public void Estimate_LowLatencyNoLoss_IsNearTheCeiling()
    {
        // Effective latency 20 + 2*2 + 10 = 34 ms; R = 93.2 - 34/40 = 92.35.
        var quality = CallQuality.Estimate(20, 2, 0);

        Assert.Equal(92.35, quality.RFactor, 6);
        Assert.Equal(4.39, quality.MeanOpinionScore, 2);
        Assert.Equal(CallQualityRating.Excellent, quality.Rating);
    }

    [Fact]
    public void Estimate_HighLatency_UsesTheSteeperSlope()
    {
        // Effective latency 200 + 0 + 10 = 210 ms; R = 93.2 - (210 - 120) / 10 = 84.2.
        var quality = CallQuality.Estimate(200, 0, 0);

        Assert.Equal(84.2, quality.RFactor, 6);
    }

    [Fact]
    public void Estimate_EachPercentOfLoss_CostsTwoAndAHalfPoints()
    {
        var clean = CallQuality.Estimate(20, 0, 0);
        var lossy = CallQuality.Estimate(20, 0, 0.04);

        Assert.Equal(clean.RFactor - 10, lossy.RFactor, 6);
    }

    [Fact]
    public void Estimate_TotalLoss_IsTheFloor()
    {
        var quality = CallQuality.Estimate(20, 0, 1);

        Assert.Equal(0, quality.RFactor);
        Assert.Equal(1, quality.MeanOpinionScore);
        Assert.Equal(CallQualityRating.Bad, quality.Rating);
    }
}
