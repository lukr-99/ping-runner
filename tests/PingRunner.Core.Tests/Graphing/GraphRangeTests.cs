using PingRunner.Core.Graphing;
using PingRunner.Core.Tests.Fakes;

namespace PingRunner.Core.Tests.Graphing;

public sealed class GraphRangeTests
{
    [Fact]
    public void Apply_RecentAttempts_KeepsTheNewest()
    {
        var range = new GraphRange("Last 2", GraphRangeMode.RecentAttempts, RecentAttempts: 2);

        var visible = range.Apply(Attempts.Series(1, 2, 3, 4));

        Assert.Equal([3L, 4L], visible.Select(attempt => attempt.RoundtripMilliseconds!.Value));
    }

    [Fact]
    public void Apply_RollingTime_KeepsTheSpanBeforeTheNewest()
    {
        var range = new GraphRange("Last 2 s", GraphRangeMode.RollingTime, TimeWindow: TimeSpan.FromSeconds(2));

        var visible = range.Apply(Attempts.Series(1, 2, 3, 4, 5));

        Assert.Equal([3L, 4L, 5L], visible.Select(attempt => attempt.RoundtripMilliseconds!.Value));
    }

    [Fact]
    public void Apply_Everything_KeepsAll()
    {
        Assert.Equal(4, GraphRange.Everything.Apply(Attempts.Series(1, 2, 3, 4)).Count);
    }

    [Fact]
    public void Presets_EndWithEverything()
    {
        Assert.Same(GraphRange.Everything, GraphRange.Presets[^1]);
    }
}
