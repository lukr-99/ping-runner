using PingRunner.Core.Pinging;

namespace PingRunner.Core.Graphing;

/// <summary>
/// How far the graph is zoomed into the ranged attempts and where the visible part sits.
/// <see cref="Factor"/> 1 shows everything; 4 shows a quarter. <see cref="Pan"/> 0 puts the visible
/// part at the oldest attempts and 1 at the newest, so a live graph zoomed in with pan 1 keeps
/// following new pings.
/// </summary>
public sealed record GraphZoom
{
    public const double MaximumFactor = 64;

    public GraphZoom(double factor, double pan)
    {
        Factor = Math.Clamp(double.IsFinite(factor) ? factor : 1, 1, MaximumFactor);
        Pan = Factor == 1 ? 1 : Math.Clamp(double.IsFinite(pan) ? pan : 1, 0, 1);
    }

    public static GraphZoom None { get; } = new(1, 1);

    public double Factor { get; }

    public double Pan { get; }

    public bool IsZoomed => Factor > 1;

    /// <summary>The visible share of the ranged attempts, 0 to 1.</summary>
    public double VisibleFraction => 1 / Factor;

    /// <summary>
    /// Zooms by <paramref name="multiplier"/> (above 1 zooms in) keeping the point at
    /// <paramref name="anchor"/> (0 is the left edge of the visible part, 1 the right) where it is.
    /// </summary>
    public GraphZoom ZoomAt(double anchor, double multiplier)
    {
        anchor = Math.Clamp(anchor, 0, 1);
        var visibleStart = (1 - VisibleFraction) * Pan;
        var anchorPosition = visibleStart + (anchor * VisibleFraction);

        var factor = Math.Clamp(Factor * multiplier, 1, MaximumFactor);
        var newVisible = 1 / factor;
        var newStart = Math.Clamp(anchorPosition - (anchor * newVisible), 0, 1 - newVisible);
        var pan = newVisible >= 1 ? 1 : newStart / (1 - newVisible);
        return new GraphZoom(factor, pan);
    }

    /// <summary>Moves the visible part by <paramref name="visibleWidths"/> of its own width (negative moves back in time).</summary>
    public GraphZoom PanBy(double visibleWidths)
    {
        if (!IsZoomed)
        {
            return this;
        }

        var hidden = 1 - VisibleFraction;
        var start = Math.Clamp((hidden * Pan) + (visibleWidths * VisibleFraction), 0, hidden);
        return new GraphZoom(Factor, start / hidden);
    }

    public IReadOnlyList<PingAttempt> Apply(IReadOnlyList<PingAttempt> attempts)
    {
        ArgumentNullException.ThrowIfNull(attempts);
        if (!IsZoomed || attempts.Count <= 2)
        {
            return attempts;
        }

        var visibleCount = Math.Max(2, (int)Math.Ceiling(attempts.Count / Factor));
        if (visibleCount >= attempts.Count)
        {
            return attempts;
        }

        var start = (int)Math.Round(Pan * (attempts.Count - visibleCount));
        return [.. attempts.Skip(start).Take(visibleCount)];
    }
}
