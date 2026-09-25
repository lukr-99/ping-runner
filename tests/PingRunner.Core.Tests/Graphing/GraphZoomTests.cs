using PingRunner.Core.Graphing;
using PingRunner.Core.Tests.Fakes;

namespace PingRunner.Core.Tests.Graphing;

public sealed class GraphZoomTests
{
    [Fact]
    public void None_ShowsEverythingAndFollowsTheNewest()
    {
        Assert.False(GraphZoom.None.IsZoomed);
        Assert.Equal(1, GraphZoom.None.Pan);
        Assert.Equal(10, GraphZoom.None.Apply(Attempts.Series(1, 2, 3, 4, 5, 6, 7, 8, 9, 10)).Count);
    }

    [Fact]
    public void Constructor_ClampsFactorAndPan()
    {
        var zoom = new GraphZoom(1_000, -3);

        Assert.Equal(GraphZoom.MaximumFactor, zoom.Factor);
        Assert.Equal(0, zoom.Pan);
    }

    [Fact]
    public void ZoomAt_KeepsTheAnchoredPointInPlace()
    {
        // Zooming 2x at the middle of everything shows the middle half.
        var zoom = GraphZoom.None.ZoomAt(0.5, 2);

        Assert.Equal(2, zoom.Factor);
        Assert.Equal(0.5, zoom.Pan, 6);
        Assert.Equal([3L, 4L, 5L, 6L], zoom.Apply(Attempts.Series(1, 2, 3, 4, 5, 6, 7, 8)).Select(attempt => attempt.RoundtripMilliseconds!.Value));
    }

    [Fact]
    public void ZoomAt_RightEdge_KeepsFollowingTheNewest()
    {
        var zoom = GraphZoom.None.ZoomAt(1, 4);

        Assert.Equal(1, zoom.Pan, 6);
    }

    [Fact]
    public void ZoomAt_ZoomingOutPastOne_ResetsToEverything()
    {
        Assert.Equal(GraphZoom.None, new GraphZoom(2, 0.3).ZoomAt(0.5, 0.25));
    }

    [Fact]
    public void PanBy_MovesByVisibleWidthsAndStopsAtTheEdges()
    {
        var zoom = new GraphZoom(4, 1);

        var back = zoom.PanBy(-1);
        Assert.Equal(2d / 3, back.Pan, 6);
        Assert.Equal(0, zoom.PanBy(-10).Pan);
        Assert.Equal(1, back.PanBy(10).Pan);
    }

    [Fact]
    public void Apply_ZoomedAtTheStart_ShowsTheOldest()
    {
        var visible = new GraphZoom(4, 0).Apply(Attempts.Series(1, 2, 3, 4, 5, 6, 7, 8));

        Assert.Equal([1L, 2L], visible.Select(attempt => attempt.RoundtripMilliseconds!.Value));
    }
}
