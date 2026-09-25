using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using PingRunner.Core.Pinging.Models;
using PingRunner.App.Features.Graphing.Models;
using PingRunner.App.Features.Graphing.Services;

namespace PingRunner.App.Controls;

public sealed class PingLatencyChart : FrameworkElement
{
    private const double MinimumChartWidth = 320;
    private const double MinimumChartHeight = 240;
    private const double LeftPadding = 58;
    private const double TopPadding = 18;
    private const double RightPadding = 18;
    private const double BottomPadding = 34;
    private static readonly Brush SurfaceBrush = CreateBrush("#FFFFFFFF");
    private static readonly Brush BorderBrush = CreateBrush("#FFD0D7E2");
    private static readonly Brush GridBrush = CreateBrush("#FFE2E8F0");
    private static readonly Brush AxisBrush = CreateBrush("#FF94A3B8");
    private static readonly Brush TextBrush = CreateBrush("#FF516074");
    private static readonly Brush PrimaryLineBrush = CreateBrush("#FF24549A");
    private static readonly Brush SuccessPointBrush = CreateBrush("#FF0F766E");
    private static readonly Brush FailurePointBrush = CreateBrush("#FFB42318");
    private static readonly Pen BorderPen = CreatePen(BorderBrush, 1);
    private static readonly Pen GridPen = CreatePen(GridBrush, 1);
    private static readonly Pen AxisPen = CreatePen(AxisBrush, 1);
    private static readonly Pen PrimaryLinePen = CreatePen(PrimaryLineBrush, 2.5);
    private INotifyCollectionChanged? _collectionNotifier;

    public PingLatencyChart()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public IEnumerable<PingAttemptResult>? ItemsSource
    {
        get => (IEnumerable<PingAttemptResult>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable<PingAttemptResult>),
            typeof(PingLatencyChart),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnItemsSourceChanged));

    public GraphRangeMode RangeMode
    {
        get => (GraphRangeMode)GetValue(RangeModeProperty);
        set => SetValue(RangeModeProperty, value);
    }

    public static readonly DependencyProperty RangeModeProperty =
        DependencyProperty.Register(
            nameof(RangeMode),
            typeof(GraphRangeMode),
            typeof(PingLatencyChart),
            new FrameworkPropertyMetadata(GraphRangeMode.RecentAttempts, FrameworkPropertyMetadataOptions.AffectsRender, OnViewportPropertyChanged));

    public int? RecentAttemptLimit
    {
        get => (int?)GetValue(RecentAttemptLimitProperty);
        set => SetValue(RecentAttemptLimitProperty, value);
    }

    public static readonly DependencyProperty RecentAttemptLimitProperty =
        DependencyProperty.Register(
            nameof(RecentAttemptLimit),
            typeof(int?),
            typeof(PingLatencyChart),
            new FrameworkPropertyMetadata(120, FrameworkPropertyMetadataOptions.AffectsRender, OnViewportPropertyChanged));

    public TimeSpan? RollingTimeWindow
    {
        get => (TimeSpan?)GetValue(RollingTimeWindowProperty);
        set => SetValue(RollingTimeWindowProperty, value);
    }

    public static readonly DependencyProperty RollingTimeWindowProperty =
        DependencyProperty.Register(
            nameof(RollingTimeWindow),
            typeof(TimeSpan?),
            typeof(PingLatencyChart),
            new FrameworkPropertyMetadata(TimeSpan.FromMinutes(5), FrameworkPropertyMetadataOptions.AffectsRender, OnViewportPropertyChanged));

    public double ZoomFactor
    {
        get => (double)GetValue(ZoomFactorProperty);
        set => SetValue(ZoomFactorProperty, value);
    }

    public static readonly DependencyProperty ZoomFactorProperty =
        DependencyProperty.Register(
            nameof(ZoomFactor),
            typeof(double),
            typeof(PingLatencyChart),
            new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender, OnViewportPropertyChanged));

    public double PanRatio
    {
        get => (double)GetValue(PanRatioProperty);
        set => SetValue(PanRatioProperty, value);
    }

    public static readonly DependencyProperty PanRatioProperty =
        DependencyProperty.Register(
            nameof(PanRatio),
            typeof(double),
            typeof(PingLatencyChart),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender, OnViewportPropertyChanged));

    protected override Size MeasureOverride(Size availableSize)
    {
        var desiredWidth = double.IsInfinity(availableSize.Width)
            ? MinimumChartWidth
            : Math.Max(0, availableSize.Width);
        var desiredHeight = double.IsInfinity(availableSize.Height)
            ? MinimumChartHeight
            : Math.Max(0, availableSize.Height);

        return new Size(desiredWidth, desiredHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        return finalSize;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var fullRect = new Rect(0, 0, ActualWidth, ActualHeight);
        drawingContext.DrawRoundedRectangle(SurfaceBrush, BorderPen, fullRect, 10, 10);

        var chartArea = new Rect(
            LeftPadding,
            TopPadding,
            Math.Max(0, ActualWidth - LeftPadding - RightPadding),
            Math.Max(0, ActualHeight - TopPadding - BottomPadding));

        if (chartArea.Width < 40 || chartArea.Height < 40)
        {
            return;
        }

        var attempts = GetRenderedAttempts();
        DrawGrid(drawingContext, chartArea);

        if (attempts.Count == 0)
        {
            DrawCenteredText(drawingContext, chartArea, "No ping data yet. Start a run to render latency history.");
            return;
        }

        var successfulLatencies = attempts
            .Where(attempt => attempt.IsSuccess && attempt.RoundtripTimeMilliseconds is not null)
            .Select(attempt => (double)attempt.RoundtripTimeMilliseconds!.Value)
            .ToList();

        var maxLatency = successfulLatencies.Count == 0
            ? 100d
            : RoundUpLatency(successfulLatencies.Max());

        DrawAxesAndLabels(drawingContext, chartArea, maxLatency, attempts);
        DrawSeries(drawingContext, chartArea, attempts, maxLatency);
    }

    private static void OnItemsSourceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not PingLatencyChart chart)
        {
            return;
        }

        chart.DetachFromCollection();

        if (chart.IsLoaded)
        {
            chart.AttachToCollection(e.NewValue as IEnumerable<PingAttemptResult>);
        }

        chart.InvalidateVisual();
    }

    private static void OnViewportPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is PingLatencyChart chart)
        {
            chart.InvalidateVisual();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachToCollection(ItemsSource);
        InvalidateVisual();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachFromCollection();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateVisual();
    }

    private void AttachToCollection(IEnumerable<PingAttemptResult>? itemsSource)
    {
        if (itemsSource is not INotifyCollectionChanged notifier || ReferenceEquals(_collectionNotifier, notifier))
        {
            return;
        }

        _collectionNotifier = notifier;
        _collectionNotifier.CollectionChanged += OnCollectionChanged;
    }

    private void DetachFromCollection()
    {
        if (_collectionNotifier is null)
        {
            return;
        }

        _collectionNotifier.CollectionChanged -= OnCollectionChanged;
        _collectionNotifier = null;
    }

    private IReadOnlyList<PingAttemptResult> GetRenderedAttempts()
    {
        if (ItemsSource is null)
        {
            return [];
        }

        var viewportAttempts = GraphViewportService.BuildViewport(
            ItemsSource,
            RangeMode,
            RecentAttemptLimit,
            RollingTimeWindow,
            ZoomFactor,
            PanRatio);

        var targetPointBudget = Math.Max(12, (int)Math.Floor(Math.Max(ActualWidth - LeftPadding - RightPadding, 120) / 3d));
        return GraphViewportService.Downsample(viewportAttempts, targetPointBudget);
    }

    private static void DrawGrid(DrawingContext drawingContext, Rect chartArea)
    {
        for (var step = 0; step <= 4; step++)
        {
            var y = chartArea.Top + (chartArea.Height * step / 4d);
            drawingContext.DrawLine(GridPen, new Point(chartArea.Left, y), new Point(chartArea.Right, y));
        }
    }

    private static void DrawAxesAndLabels(
        DrawingContext drawingContext,
        Rect chartArea,
        double maxLatency,
        IReadOnlyList<PingAttemptResult> attempts)
    {
        drawingContext.DrawLine(AxisPen, new Point(chartArea.Left, chartArea.Bottom), new Point(chartArea.Right, chartArea.Bottom));
        drawingContext.DrawLine(AxisPen, new Point(chartArea.Left, chartArea.Top), new Point(chartArea.Left, chartArea.Bottom));

        for (var step = 0; step <= 4; step++)
        {
            var labelValue = maxLatency - (maxLatency * step / 4d);
            var y = chartArea.Top + (chartArea.Height * step / 4d) - 8;
            DrawText(drawingContext, $"{labelValue:0} ms", new Point(8, y), 12, FontWeights.Medium);
        }

        var firstTimestamp = attempts[0].Timestamp.ToLocalTime().ToString("HH:mm:ss");
        var lastTimestamp = attempts[^1].Timestamp.ToLocalTime().ToString("HH:mm:ss");
        DrawText(drawingContext, firstTimestamp, new Point(chartArea.Left, chartArea.Bottom + 8), 11, FontWeights.Normal);
        DrawText(drawingContext, lastTimestamp, new Point(chartArea.Right - 52, chartArea.Bottom + 8), 11, FontWeights.Normal);

        var rangeLabel = attempts.Count == 1
            ? "1 visible attempt"
            : $"{attempts.Count} visible attempts";

        DrawText(
            drawingContext,
            rangeLabel,
            new Point(chartArea.Left + (chartArea.Width / 2d) - 44, chartArea.Bottom + 8),
            11,
            FontWeights.Medium);
    }

    private static void DrawSeries(
        DrawingContext drawingContext,
        Rect chartArea,
        IReadOnlyList<PingAttemptResult> attempts,
        double maxLatency)
    {
        var currentSegment = new List<Point>();

        for (var index = 0; index < attempts.Count; index++)
        {
            var attempt = attempts[index];
            var x = GetXPosition(chartArea, index, attempts.Count);

            if (attempt.IsSuccess && attempt.RoundtripTimeMilliseconds is { } latency)
            {
                var y = GetYPosition(chartArea, latency, maxLatency);
                var point = new Point(x, y);
                currentSegment.Add(point);
                drawingContext.DrawEllipse(SuccessPointBrush, null, point, 3, 3);
                continue;
            }

            DrawSegment(drawingContext, currentSegment);
            currentSegment.Clear();

            var failurePoint = new Point(x, chartArea.Bottom - 4);
            drawingContext.DrawEllipse(FailurePointBrush, null, failurePoint, 4, 4);
        }

        DrawSegment(drawingContext, currentSegment);
    }

    private static void DrawSegment(DrawingContext drawingContext, IReadOnlyList<Point> points)
    {
        if (points.Count < 2)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using var geometryContext = geometry.Open();
        geometryContext.BeginFigure(points[0], false, false);
        geometryContext.PolyLineTo(points.Skip(1).ToArray(), true, false);
        geometry.Freeze();

        drawingContext.DrawGeometry(null, PrimaryLinePen, geometry);
    }

    private static double GetXPosition(Rect chartArea, int index, int totalCount)
    {
        if (totalCount <= 1)
        {
            return chartArea.Left + (chartArea.Width / 2d);
        }

        return chartArea.Left + (chartArea.Width * index / (totalCount - 1d));
    }

    private static double GetYPosition(Rect chartArea, double latency, double maxLatency)
    {
        var normalizedLatency = Math.Clamp(latency / maxLatency, 0d, 1d);
        return chartArea.Bottom - (normalizedLatency * chartArea.Height);
    }

    private static double RoundUpLatency(double value)
    {
        var roundedBase = value switch
        {
            <= 50 => 10d,
            <= 200 => 25d,
            <= 500 => 50d,
            _ => 100d,
        };

        return Math.Max(roundedBase, Math.Ceiling(value / roundedBase) * roundedBase);
    }

    private static void DrawCenteredText(DrawingContext drawingContext, Rect chartArea, string text)
    {
        var formattedText = CreateText(text, 14, FontWeights.Medium);
        var location = new Point(
            chartArea.Left + ((chartArea.Width - formattedText.Width) / 2d),
            chartArea.Top + ((chartArea.Height - formattedText.Height) / 2d));

        drawingContext.DrawText(formattedText, location);
    }

    private static void DrawText(
        DrawingContext drawingContext,
        string text,
        Point location,
        double fontSize,
        FontWeight fontWeight)
    {
        drawingContext.DrawText(CreateText(text, fontSize, fontWeight), location);
    }

    private static FormattedText CreateText(string text, double fontSize, FontWeight fontWeight)
    {
        Visual dpiSource = Application.Current?.MainWindow is Visual visual
            ? visual
            : new DrawingVisual();

        return new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, fontWeight, FontStretches.Normal),
            fontSize,
            TextBrush,
            VisualTreeHelper.GetDpi(dpiSource).PixelsPerDip);
    }

    private static Brush CreateBrush(string colorHex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(colorHex)!;
        brush.Freeze();
        return brush;
    }

    private static Pen CreatePen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness);
        pen.Freeze();
        return pen;
    }
}
