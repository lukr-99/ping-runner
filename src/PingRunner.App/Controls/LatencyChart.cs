using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using PingRunner.Core.Formatting;
using PingRunner.Core.Graphing;
using PingRunner.Core.Pinging;

namespace PingRunner.App.Controls;

/// <summary>
/// Round-trip times as a line over the attempts, in order. Lost pings break the line and show as red
/// bands, so an outage reads as a red stretch. It draws <see cref="Attempts"/> through
/// <see cref="Zoom"/>, thinned to about one point per two pixels without losing spikes or failures.
/// When interactive, the wheel zooms at the pointer, dragging pans, double-click resets, and hovering
/// reads out the nearest attempt. Colors come from the theme's semantic brushes.
/// </summary>
public sealed class LatencyChart : FrameworkElement
{
    public static readonly DependencyProperty AttemptsProperty = DependencyProperty.Register(
        nameof(Attempts), typeof(IReadOnlyList<PingAttempt>), typeof(LatencyChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, (chart, _) => ((LatencyChart)chart).hover = null));

    public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(
        nameof(Zoom), typeof(GraphZoom), typeof(LatencyChart),
        new FrameworkPropertyMetadata(GraphZoom.None, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty IsInteractiveProperty = DependencyProperty.Register(
        nameof(IsInteractive), typeof(bool), typeof(LatencyChart), new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty ShowAverageProperty = DependencyProperty.Register(
        nameof(ShowAverage), typeof(bool), typeof(LatencyChart), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty EmptyTextProperty = DependencyProperty.Register(
        nameof(EmptyText), typeof(string), typeof(LatencyChart),
        new FrameworkPropertyMetadata("No pings yet. Start a run to see latency here.", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LineBrushProperty = BrushProperty(nameof(LineBrush));
    public static readonly DependencyProperty FillBrushProperty = BrushProperty(nameof(FillBrush));
    public static readonly DependencyProperty FailureBrushProperty = BrushProperty(nameof(FailureBrush));
    public static readonly DependencyProperty FailureBandBrushProperty = BrushProperty(nameof(FailureBandBrush));
    public static readonly DependencyProperty GridBrushProperty = BrushProperty(nameof(GridBrush));
    public static readonly DependencyProperty TextBrushProperty = BrushProperty(nameof(TextBrush));
    public static readonly DependencyProperty LabelBrushProperty = BrushProperty(nameof(LabelBrush));
    public static readonly DependencyProperty TooltipBrushProperty = BrushProperty(nameof(TooltipBrush));

    private const double LeftPadding = 56;
    private const double TopPadding = 12;
    private const double RightPadding = 14;
    private const double BottomPadding = 26;

    private Point? dragStart;
    private GraphZoom? dragZoom;
    private Point? hover;

    public LatencyChart()
    {
        SetResourceReference(LineBrushProperty, "PR.AccentBrush");
        SetResourceReference(FillBrushProperty, "PR.AccentSoftBrush");
        SetResourceReference(FailureBrushProperty, "PR.DangerBrush");
        SetResourceReference(FailureBandBrushProperty, "PR.DangerSoftBrush");
        SetResourceReference(GridBrushProperty, "PR.BorderBrush");
        SetResourceReference(TextBrushProperty, "PR.TextSecondaryBrush");
        SetResourceReference(LabelBrushProperty, "PR.TextPrimaryBrush");
        SetResourceReference(TooltipBrushProperty, "PR.SurfaceRaisedBrush");
        ClipToBounds = true;
        Focusable = false;
    }

    /// <summary>The attempts in the chosen range, oldest first; the zoom picks the visible part.</summary>
    public IReadOnlyList<PingAttempt>? Attempts
    {
        get => (IReadOnlyList<PingAttempt>?)GetValue(AttemptsProperty);
        set => SetValue(AttemptsProperty, value);
    }

    public GraphZoom Zoom
    {
        get => (GraphZoom)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public bool IsInteractive
    {
        get => (bool)GetValue(IsInteractiveProperty);
        set => SetValue(IsInteractiveProperty, value);
    }

    public bool ShowAverage
    {
        get => (bool)GetValue(ShowAverageProperty);
        set => SetValue(ShowAverageProperty, value);
    }

    public string EmptyText
    {
        get => (string)GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }

    public Brush? LineBrush { get => (Brush?)GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }

    public Brush? FillBrush { get => (Brush?)GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }

    public Brush? FailureBrush { get => (Brush?)GetValue(FailureBrushProperty); set => SetValue(FailureBrushProperty, value); }

    public Brush? FailureBandBrush { get => (Brush?)GetValue(FailureBandBrushProperty); set => SetValue(FailureBandBrushProperty, value); }

    public Brush? GridBrush { get => (Brush?)GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }

    public Brush? TextBrush { get => (Brush?)GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }

    public Brush? LabelBrush { get => (Brush?)GetValue(LabelBrushProperty); set => SetValue(LabelBrushProperty, value); }

    public Brush? TooltipBrush { get => (Brush?)GetValue(TooltipBrushProperty); set => SetValue(TooltipBrushProperty, value); }

    protected override Size MeasureOverride(Size availableSize) => new(
        double.IsInfinity(availableSize.Width) ? 320 : availableSize.Width,
        double.IsInfinity(availableSize.Height) ? 200 : availableSize.Height);

    protected override void OnRender(DrawingContext drawingContext)
    {
        drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        var area = new Rect(
            LeftPadding,
            TopPadding,
            Math.Max(0, ActualWidth - LeftPadding - RightPadding),
            Math.Max(0, ActualHeight - TopPadding - BottomPadding));
        if (area.Width < 40 || area.Height < 30)
        {
            return;
        }

        var visible = Zoom.Apply(Attempts ?? []);
        var replies = visible.Where(attempt => attempt.RoundtripMilliseconds is not null).Select(attempt => (double)attempt.RoundtripMilliseconds!.Value).ToList();
        var (top, step) = ChartText.Axis(replies.Count == 0 ? 0 : replies.Max(), 10);

        DrawGrid(drawingContext, area, top, step);
        if (visible.Count == 0)
        {
            var empty = Text(EmptyText, 13, TextBrush, FontWeights.Normal);
            drawingContext.DrawText(empty, new Point(area.Left + ((area.Width - empty.Width) / 2), area.Top + ((area.Height - empty.Height) / 2)));
            return;
        }

        var positions = PositionsOf(visible, LatencyDownsampler.Downsample(visible, Math.Max(16, (int)(area.Width / 2))));
        DrawFailures(drawingContext, area, visible.Count, positions);
        DrawSeries(drawingContext, area, visible.Count, positions, top);
        if (ShowAverage && replies.Count > 1)
        {
            DrawAverage(drawingContext, area, replies.Average(), top);
        }

        DrawTimeLabels(drawingContext, area, visible);
        if (hover is { } pointer && IsInteractive && dragStart is null)
        {
            DrawHover(drawingContext, area, visible, pointer, top);
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (!IsInteractive || Attempts is not { Count: > 2 })
        {
            return;
        }

        var anchor = (e.GetPosition(this).X - LeftPadding) / Math.Max(1, ActualWidth - LeftPadding - RightPadding);
        Zoom = Zoom.ZoomAt(anchor, e.Delta > 0 ? 1.25 : 0.8);
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (!IsInteractive)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            Zoom = GraphZoom.None;
            e.Handled = true;
            return;
        }

        if (Zoom.IsZoomed && CaptureMouse())
        {
            dragStart = e.GetPosition(this);
            dragZoom = Zoom;
            Cursor = Cursors.SizeWE;
            e.Handled = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!IsInteractive)
        {
            return;
        }

        var position = e.GetPosition(this);
        if (dragStart is { } start && dragZoom is { } zoom)
        {
            var width = Math.Max(1, ActualWidth - LeftPadding - RightPadding);
            Zoom = zoom.PanBy(-(position.X - start.X) / width);
            return;
        }

        hover = position;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        EndDrag();
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        EndDrag();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        hover = null;
        InvalidateVisual();
    }

    private static DependencyProperty BrushProperty(string name) => DependencyProperty.Register(
        name, typeof(Brush), typeof(LatencyChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    // Where each kept attempt sits among the visible ones, so thinning never shifts it sideways.
    private static List<(int Index, PingAttempt Attempt)> PositionsOf(IReadOnlyList<PingAttempt> visible, IReadOnlyList<PingAttempt> kept)
    {
        var positions = new List<(int, PingAttempt)>(kept.Count);
        var cursor = 0;
        foreach (var attempt in kept)
        {
            while (cursor < visible.Count && !ReferenceEquals(visible[cursor], attempt))
            {
                cursor++;
            }

            positions.Add((Math.Min(cursor, visible.Count - 1), attempt));
        }

        return positions;
    }

    private static double X(Rect area, int index, int count) =>
        count <= 1 ? area.Left + (area.Width / 2) : area.Left + (area.Width * index / (count - 1d));

    private static double Y(Rect area, double latency, double top) =>
        area.Bottom - (Math.Clamp(latency / top, 0, 1) * area.Height);

    private void DrawGrid(DrawingContext drawingContext, Rect area, double top, double step)
    {
        var gridPen = Frozen(new Pen(GridBrush, 1));
        for (var value = 0d; value <= top + (step / 2); value += step)
        {
            var y = Math.Round(Y(area, value, top)) + 0.5;
            drawingContext.DrawLine(gridPen, new Point(area.Left, y), new Point(area.Right, y));
            var label = Text(Units.Milliseconds(value).Replace(".0 ms", " ms", StringComparison.Ordinal), 11, TextBrush, FontWeights.Normal);
            drawingContext.DrawText(label, new Point(area.Left - label.Width - 8, y - (label.Height / 2)));
        }
    }

    private void DrawFailures(DrawingContext drawingContext, Rect area, int count, List<(int Index, PingAttempt Attempt)> positions)
    {
        var bandWidth = Math.Max(2, area.Width / Math.Max(1, count));
        foreach (var (index, attempt) in positions.Where(position => !position.Attempt.IsSuccess))
        {
            var x = X(area, index, count);
            drawingContext.DrawRectangle(FailureBandBrush, null, new Rect(x - (bandWidth / 2), area.Top, bandWidth, area.Height));
            drawingContext.DrawEllipse(FailureBrush, null, new Point(x, area.Bottom - 4), 3, 3);
        }
    }

    private void DrawSeries(DrawingContext drawingContext, Rect area, int count, List<(int Index, PingAttempt Attempt)> positions, double top)
    {
        var linePen = Frozen(new Pen(LineBrush, 2) { LineJoin = PenLineJoin.Round });
        var segment = new List<Point>();

        void Flush()
        {
            if (segment.Count == 1)
            {
                drawingContext.DrawEllipse(LineBrush, null, segment[0], 2.5, 2.5);
            }
            else if (segment.Count > 1)
            {
                drawingContext.DrawGeometry(FillBrush, null, Polygon(segment, area.Bottom));
                drawingContext.DrawGeometry(null, linePen, Polyline(segment));
            }

            segment.Clear();
        }

        foreach (var (index, attempt) in positions)
        {
            if (attempt.RoundtripMilliseconds is { } roundtrip)
            {
                segment.Add(new Point(X(area, index, count), Y(area, roundtrip, top)));
            }
            else
            {
                Flush();
            }
        }

        Flush();
    }

    private void DrawAverage(DrawingContext drawingContext, Rect area, double average, double top)
    {
        var y = Y(area, average, top);
        var pen = Frozen(new Pen(LabelBrush, 1) { DashStyle = new DashStyle([4, 4], 0) });
        drawingContext.PushOpacity(0.55);
        drawingContext.DrawLine(pen, new Point(area.Left, y), new Point(area.Right, y));
        drawingContext.Pop();
        var label = Text($"avg {Units.Milliseconds(average)}", 11, LabelBrush, FontWeights.SemiBold);
        var box = new Rect(area.Right - label.Width - 10, Math.Max(area.Top, y - label.Height - 3), label.Width + 8, label.Height + 2);
        drawingContext.DrawRoundedRectangle(TooltipBrush, null, box, 4, 4);
        drawingContext.DrawText(label, new Point(box.Left + 4, box.Top + 1));
    }

    private void DrawTimeLabels(DrawingContext drawingContext, Rect area, IReadOnlyList<PingAttempt> visible)
    {
        var format = visible[^1].Timestamp - visible[0].Timestamp > TimeSpan.FromDays(1) ? "d MMM HH:mm" : "HH:mm:ss";
        var first = Text(visible[0].Timestamp.ToLocalTime().ToString(format, System.Globalization.CultureInfo.CurrentCulture), 11, TextBrush, FontWeights.Normal);
        drawingContext.DrawText(first, new Point(area.Left, area.Bottom + 6));
        if (visible.Count > 1)
        {
            var last = Text(visible[^1].Timestamp.ToLocalTime().ToString(format, System.Globalization.CultureInfo.CurrentCulture), 11, TextBrush, FontWeights.Normal);
            drawingContext.DrawText(last, new Point(area.Right - last.Width, area.Bottom + 6));
            var count = Text($"{Units.Count(visible.Count)} pings", 11, TextBrush, FontWeights.Normal);
            drawingContext.DrawText(count, new Point(area.Left + ((area.Width - count.Width) / 2), area.Bottom + 6));
        }
    }

    private void DrawHover(DrawingContext drawingContext, Rect area, IReadOnlyList<PingAttempt> visible, Point pointer, double top)
    {
        if (!area.Contains(pointer))
        {
            return;
        }

        var index = visible.Count <= 1 ? 0 : (int)Math.Round((pointer.X - area.Left) / area.Width * (visible.Count - 1));
        var attempt = visible[Math.Clamp(index, 0, visible.Count - 1)];
        var x = X(area, Math.Clamp(index, 0, visible.Count - 1), visible.Count);
        drawingContext.DrawLine(Frozen(new Pen(TextBrush, 1)), new Point(x, area.Top), new Point(x, area.Bottom));

        var value = attempt.RoundtripMilliseconds is { } roundtrip ? $"{roundtrip} ms" : "Lost";
        if (attempt.RoundtripMilliseconds is { } dot)
        {
            drawingContext.DrawEllipse(LineBrush, Frozen(new Pen(TooltipBrush, 2)), new Point(x, Y(area, dot, top)), 4.5, 4.5);
        }

        var heading = Text(value, 13, attempt.IsSuccess ? LabelBrush : FailureBrush, FontWeights.SemiBold);
        var detail = Text(
            $"{attempt.Timestamp.ToLocalTime():HH:mm:ss}  ·  {(attempt.IsSuccess ? attempt.TargetHost : attempt.Details)}",
            11,
            TextBrush,
            FontWeights.Normal);
        var width = Math.Max(heading.Width, detail.Width) + 16;
        var height = heading.Height + detail.Height + 10;
        var left = x + 12 + width > area.Right ? x - 12 - width : x + 12;
        var box = new Rect(left, area.Top + 6, width, height);
        drawingContext.DrawRoundedRectangle(TooltipBrush, Frozen(new Pen(GridBrush, 1)), box, 6, 6);
        drawingContext.DrawText(heading, new Point(box.Left + 8, box.Top + 5));
        drawingContext.DrawText(detail, new Point(box.Left + 8, box.Top + 5 + heading.Height));
    }

    private void EndDrag()
    {
        if (dragStart is null)
        {
            return;
        }

        dragStart = null;
        dragZoom = null;
        Cursor = null;
        ReleaseMouseCapture();
        InvalidateVisual();
    }

    private System.Windows.Media.FormattedText Text(string text, double size, Brush? brush, FontWeight weight) =>
        ChartText.Create(this, this, text, size, brush ?? Brushes.Gray, weight);

    private static StreamGeometry Polyline(List<Point> points)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(points[0], false, false);
            context.PolyLineTo([.. points.Skip(1)], true, true);
        }

        geometry.Freeze();
        return geometry;
    }

    private static StreamGeometry Polygon(List<Point> points, double baseline)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(points[0].X, baseline), true, true);
            context.PolyLineTo([.. points, new Point(points[^1].X, baseline)], false, false);
        }

        geometry.Freeze();
        return geometry;
    }

    private static Pen Frozen(Pen pen)
    {
        pen.Freeze();
        return pen;
    }
}
