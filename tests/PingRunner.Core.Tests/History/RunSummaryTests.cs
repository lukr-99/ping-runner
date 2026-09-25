using PingRunner.Core.History;
using PingRunner.Core.Statistics;
using PingRunner.Core.Tests.Fakes;

namespace PingRunner.Core.Tests.History;

public sealed class RunSummaryTests
{
    [Fact]
    public void From_Statistics_KeepsTheHeadlineFigures()
    {
        var summary = RunSummary.From(PingStatistics.From(Attempts.Series(10, null, null, 30, 20)));

        Assert.Equal(5, summary.Sent);
        Assert.Equal(3, summary.Received);
        Assert.Equal(0.4, summary.LossFraction);
        Assert.Equal(20, summary.MeanMilliseconds);
        Assert.Equal(20, summary.MedianMilliseconds);
        Assert.Equal(1, summary.Outages);
        Assert.Equal(TimeSpan.FromSeconds(2), summary.LongestOutage);
        Assert.NotNull(summary.MeanOpinionScore);
    }

    [Fact]
    public void Empty_HasNoFigures()
    {
        Assert.Null(RunSummary.Empty.LossFraction);
        Assert.Null(RunSummary.Empty.MeanMilliseconds);
    }
}
