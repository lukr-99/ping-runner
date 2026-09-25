using System.Windows;
using System.Windows.Media;
using PingRunner.App.Formatting;
using PingRunner.Core.Throughput;

namespace PingRunner.App.Controls;

/// <summary>
/// Download and upload rates over the seconds of a speed test, both on one Mbps axis: download in the
/// accent color, upload in the secondary text color.
/// </summary>
public sealed class ThroughputChart : FrameworkElement
{
    public static readonly DependencyProperty DownloadProperty = DependencyProperty.Register(
        nameof(Download), typeof(IReadOnlyList<ThroughputPoint>), typeof(ThroughputChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty UploadProperty = DependencyProperty.Register(
        nameof(Upload), typeof(IReadOnlyList<ThroughputPoint>), typeof(ThroughputChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DurationProperty = DependencyProperty.Register(
        nameof(Duration), typeof(TimeSpan), typeof(ThroughputChart),
        new FrameworkPropertyMetadata(TimeSpan.FromSeconds(10), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DownloadBrushProperty = BrushProperty(nameof(DownloadBrush));
    public static readonly DependencyProperty DownloadFillProperty = BrushProperty(nameof(DownloadFill));
    public static readonly DependencyProperty UploadBrushProperty = BrushProperty(nameof(UploadBrush));
    public static readonly DependencyProperty GridBrushProperty = BrushProperty(nameof(GridBrush));
    public static readonly DependencyProperty TextBrushProperty = BrushProperty(nameof(TextBrush));

    private const double LeftPadding = 64;
    private const double TopPadding = 10;
    private const double RightPadding = 12;
    private const double BottomPadding = 24;

    public ThroughputChart()
    {
        SetResourceReference(DownloadBrushProperty, "PR.AccentBrush");
        SetResourceReference(DownloadFillProperty, "PR.AccentSoftBrush");
        SetResourceReference(UploadBrushProperty, "PR.TextSecondaryBrush");
        SetResourceReference(GridBrushProperty, "PR.BorderBrush");
        SetResourceReference(TextBrushProperty, "PR.TextSecondaryBrush");
        ClipToBounds = true;
    }

    public IReadOnlyList<ThroughputPoint>? Download
    {
        get => (IReadOnlyList<ThroughputPoint>?)GetValue(DownloadProperty);
        set => SetValue(DownloadProperty, value);
    }

    public IReadOnlyList<ThroughputPoint>? Upload
    {
        get => (IReadOnlyList<ThroughputPoint>?)GetValue(UploadProperty);
        set => SetValue(UploadProperty, value);
    }

    /// <summary>The length of one direction's test: the time axis.</summary>
    public TimeSpan Duration
    {
        get => (TimeSpan)GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    public Brush? DownloadBrush { get => (Brush?)GetValue(DownloadBrushProperty); set => SetValue(DownloadBrushProperty, value); }

    public Brush? DownloadFill { get => (Brush?)GetValue(DownloadFillProperty); set => SetValue(DownloadFillProperty, value); }

    public Brush? UploadBrush { get => (Brush?)GetValue(UploadBrushProperty); set => SetValue(UploadBrushProperty, value); }

    public Brush? GridBrush { get => (Brush?)GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }

    public Brush? TextBrush { get => (Brush?)GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }

    protected override Size MeasureOverride(Size availableSize) => new(
        double.IsInfinity(availableSize.Width) ? 320 : availableSize.Width,
        double.IsInfinity(availableSize.Height) ? 160 : availableSize.Height);

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

        var download = Download ?? [];
        var upload = Upload ?? [];
        var peak = download.Concat(upload).Select(point => point.BitsPerSecond).DefaultIfEmpty(0).Max();
        var (top, step) = ChartText.Axis(peak / 1e6, 10);
        var seconds = Math.Max(1, Duration.TotalSeconds);

        var gridPen = new Pen(GridBrush, 1);
        gridPen.Freeze();
        for (var value = 0d; value <= top + (step / 2); value += step)
        {
            var y = Math.Round(area.Bottom - (value / top * area.Height)) + 0.5;
            drawingContext.DrawLine(gridPen, new Point(area.Left, y), new Point(area.Right, y));
            var label = ChartText.Create(this, this, $"{value:0.#} Mbps", 11, TextBrush ?? Brushes.Gray, FontWeights.Normal);
            drawingContext.DrawText(label, new Point(area.Left - label.Width - 8, y - (label.Height / 2)));
        }

        var start = ChartText.Create(this, this, "0 s", 11, TextBrush ?? Brushes.Gray, FontWeights.Normal);
        drawingContext.DrawText(start, new Point(area.Left, area.Bottom + 5));
        var end = ChartText.Create(this, this, Units.Span(Duration).Replace(".0 s", " s", StringComparison.Ordinal), 11, TextBrush ?? Brushes.Gray, FontWeights.Normal);
        drawingContext.DrawText(end, new Point(area.Right - end.Width, area.Bottom + 5));

        Point ToPoint(ThroughputPoint point) => new(
            area.Left + (Math.Clamp(point.Elapsed.TotalSeconds / seconds, 0, 1) * area.Width),
            area.Bottom - (Math.Clamp(point.BitsPerSecond / 1e6 / top, 0, 1) * area.Height));

        DrawSeries(drawingContext, [.. upload.Select(ToPoint)], UploadBrush, null, area.Bottom, dashed: true);
        DrawSeries(drawingContext, [.. download.Select(ToPoint)], DownloadBrush, DownloadFill, area.Bottom, dashed: false);
    }

    private static DependencyProperty BrushProperty(string name) => DependencyProperty.Register(
        name, typeof(Brush), typeof(ThroughputChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    private static void DrawSeries(DrawingContext drawingContext, List<Point> points, Brush? stroke, Brush? fill, double baseline, bool dashed)
    {
        if (points.Count < 2)
        {
            return;
        }

        if (fill is not null)
        {
            var area = new StreamGeometry();
            using (var context = area.Open())
            {
                context.BeginFigure(new Point(points[0].X, baseline), true, true);
                context.PolyLineTo([.. points, new Point(points[^1].X, baseline)], false, false);
            }

            area.Freeze();
            drawingContext.DrawGeometry(fill, null, area);
        }

        var line = new StreamGeometry();
        using (var context = line.Open())
        {
            context.BeginFigure(points[0], false, false);
            context.PolyLineTo([.. points.Skip(1)], true, true);
        }

        line.Freeze();
        var pen = new Pen(stroke, 2) { LineJoin = PenLineJoin.Round };
        if (dashed)
        {
            pen.DashStyle = new DashStyle([3, 2], 0);
        }

        pen.Freeze();
        drawingContext.DrawGeometry(null, pen, line);
    }
}
