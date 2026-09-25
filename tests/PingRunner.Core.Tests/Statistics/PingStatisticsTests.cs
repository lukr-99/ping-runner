using PingRunner.Core.Statistics;
using PingRunner.Core.Tests.Fakes;

namespace PingRunner.Core.Tests.Statistics;

public sealed class PingStatisticsTests
{
    [Fact]
    public void From_NoAttempts_IsEmpty()
    {
        var statistics = PingStatistics.From([]);

        Assert.Equal(0, statistics.Sent);
        Assert.Null(statistics.LossFraction);
        Assert.Null(statistics.Latency);
        Assert.Null(statistics.CallQuality);
    }

    [Fact]
    public void From_MixedAttempts_CountsLoss()
    {
        var statistics = PingStatistics.From(Attempts.Series(20, null, 22, 24));

        Assert.Equal(4, statistics.Sent);
        Assert.Equal(3, statistics.Received);
        Assert.Equal(1, statistics.Lost);
        Assert.Equal(0.25, statistics.LossFraction);
        Assert.Equal(22, statistics.Latency!.Mean);
    }

    [Fact]
    public void From_Replies_JitterIsTheMeanChangeBetweenReplies()
    {
        // Changes: |30-10| = 20, then the lost ping is skipped, |20-30| = 10.
        var statistics = PingStatistics.From(Attempts.Series(10, 30, null, 20));

        Assert.Equal(15, statistics.JitterMilliseconds);
    }

    [Fact]
    public void From_OneReply_HasNoJitter()
    {
        Assert.Null(PingStatistics.From(Attempts.Series(10, null)).JitterMilliseconds);
    }

    [Fact]
    public void From_SingleLostPing_IsNotAnOutage()
    {
        var statistics = PingStatistics.From(Attempts.Series(10, null, 10, null, 10));

        Assert.Empty(statistics.Outages);
    }

    [Fact]
    public void From_LostRunEndedByReply_OutageLastsUntilTheReply()
    {
        var statistics = PingStatistics.From(Attempts.Series(10, null, null, null, 10, null, null, 10));

        Assert.Equal(2, statistics.Outages.Count);
        var longest = statistics.LongestOutage!;
        Assert.Equal(Attempts.Start.AddSeconds(1), longest.Start);
        Assert.Equal(TimeSpan.FromSeconds(3), longest.Duration);
        Assert.Equal(3, longest.LostCount);
        Assert.False(longest.IsOngoing);
    }

    [Fact]
    public void From_TrailingLostRun_IsOngoing()
    {
        var statistics = PingStatistics.From(Attempts.Series(10, null, null, null));

        var outage = Assert.Single(statistics.Outages);
        Assert.True(outage.IsOngoing);
        Assert.Equal(TimeSpan.FromSeconds(2), outage.Duration);
    }

    [Fact]
    public void From_Attempts_SpanCoversFirstToLast()
    {
        var statistics = PingStatistics.From(Attempts.Series(10, 10, 10, 10, 10));

        Assert.Equal(TimeSpan.FromSeconds(4), statistics.Span);
    }

    [Fact]
    public void From_CleanFastConnection_RatesCallQualityExcellent()
    {
        var statistics = PingStatistics.From(Attempts.Series(15, 16, 15, 17, 15));

        Assert.Equal(CallQualityRating.Excellent, statistics.CallQuality!.Rating);
    }
}
