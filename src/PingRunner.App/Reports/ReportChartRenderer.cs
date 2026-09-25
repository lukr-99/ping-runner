using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PingRunner.App.Controls;
using PingRunner.App.Theming;
using PingRunner.Core.Formatting;
using PingRunner.Core.Reports;

namespace PingRunner.App.Reports;

/// <summary>
/// Draws a report's charts as PNGs for the PDF and Excel writers: latency over time with lost pings
/// and outages marked, loss per slice, how replies spread, and the speed tests. Charts are drawn off
/// screen at twice their layout size in the light palette and the report's accent, whatever the app's
/// theme, because reports are printed and passed on. Times are the report's measured offset.
/// </summary>
public sealed class ReportChartRenderer
{
    public const int Width = 800;
    public const int Height = 320;
    private const double Scale = 2;
    private const int TimeColumns = 400;
    private const int MaximumSpeedTests = 12;

    private static readonly TimeSpan[] TickSteps =
    [
        TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(1), TimeSpan.FromHours(2), TimeSpan.FromHours(3), TimeSpan.FromHours(6), TimeSpan.FromHours(12),
        TimeSpan.FromDays(1), TimeSpan.FromDays(2), TimeSpan.FromDays(7),
    ];

    private static readonly Typeface Regular = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private static readonly Typeface SemiBold = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

    public IReadOnlyList<ReportChart> Draw(ConnectionReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var palette = Palette.For(report.AccentColor);
        var charts = new List<ReportChart> { LatencyOverTime(report, palette) };
        if (report.Buckets.Count > 1)
        {
            charts.Add(LossPerSlice(report, palette));
        }

        if (report.Statistics.Latency is { Count: > 1 })
        {
            charts.Add(LatencySpread(report, palette));
        }

        if (report.SpeedTests.Count > 0)
        {
            charts.Add(SpeedTests(report, palette));
        }

        return charts;
    }

    private static ReportChart LatencyOverTime(ConnectionReport report, Palette palette)
    {
        var from = report.From;
        var span = report.Span > TimeSpan.Zero ? report.Span : TimeSpan.FromSeconds(1);
        var columns = Math.Clamp(report.Attempts.Count, 1, TimeColumns);
        var slot = span.Ticks / (double)columns;
        var sums = new double[columns];
        var replies = new int[columns];
        var slowest = new double[columns];
        var sent = new int[columns];
        var lost = new int[columns];
        foreach (var attempt in report.Attempts)
        {
            var column = Math.Clamp((int)((attempt.Timestamp - from).Ticks / slot), 0, columns - 1);
            sent[column]++;
            if (attempt.RoundtripMilliseconds is { } roundtrip)
            {
                sums[column] += roundtrip;
                replies[column]++;
                slowest[column] = Math.Max(slowest[column], roundtrip);
            }
            else if (!attempt.IsSuccess)
            {
                lost[column]++;
            }
        }

        // One huge spike should not flatten everything else: the axis stops at twice the 99th percentile.
        var latency = report.Statistics.Latency;
        var limit = latency is null ? 10 : Math.Max(20, latency.Percentile(99) * 2);
        var clipped = latency is not null && latency.Maximum > limit;
        var (top, step) = ChartText.Axis(latency is null ? 0 : Math.Min(latency.Maximum, limit), 10);
        var caption = $"Each of the {Units.Count(columns)} columns covers {Units.Span(TimeSpan.FromTicks((long)slot))}: the line is the mean reply "
            + "and the shaded band reaches the slowest one. Red columns lost pings, darker red spans are outages."
            + (clipped ? $" Replies above {Units.Milliseconds(top)} are cut off at the top (the slowest took {Units.Milliseconds(latency!.Maximum)})." : string.Empty);

        return Render("Latency over time", caption, palette, (context, area) =>
        {
            DrawValueGrid(context, area, palette, top, step, value => Units.Milliseconds(value).Replace(".0 ms", " ms", StringComparison.Ordinal));
            double X(DateTimeOffset time) => area.Left + (area.Width * Math.Clamp((time - from).Ticks / (double)span.Ticks, 0, 1));
            double ColumnX(int column) => area.Left + (area.Width * (column + 0.5) / columns);
            double Y(double value) => area.Bottom - (Math.Clamp(value / top, 0, 1) * area.Height);
            var columnWidth = area.Width / columns;

            for (var column = 0; column < columns; column++)
            {
                if (lost[column] > 0)
                {
                    var share = lost[column] / (double)sent[column];
                    context.PushOpacity(0.12 + (0.3 * share));
                    context.DrawRectangle(palette.Danger, null, new Rect(area.Left + (column * columnWidth), area.Top, Math.Max(1, columnWidth), area.Height));
                    context.Pop();
                }
            }

            foreach (var outage in report.Statistics.Outages)
            {
                var left = X(outage.Start);
                var width = Math.Max(2, X(outage.End) - left);
                context.PushOpacity(0.35);
                context.DrawRectangle(palette.Danger, null, new Rect(left, area.Top, width, area.Height));
                context.Pop();
                context.DrawRectangle(palette.Danger, null, new Rect(left, area.Bottom - 4, width, 4));
            }

            foreach (var segment in Segments(columns, column => replies[column] > 0))
            {
                var band = new StreamGeometry();
                using (var geometry = band.Open())
                {
                    geometry.BeginFigure(new Point(ColumnX(segment[0]), Y(slowest[segment[0]])), true, true);
                    foreach (var column in segment.Skip(1))
                    {
                        geometry.LineTo(new Point(ColumnX(column), Y(slowest[column])), true, true);
                    }

                    foreach (var column in Enumerable.Reverse(segment))
                    {
                        geometry.LineTo(new Point(ColumnX(column), Y(sums[column] / replies[column])), true, true);
                    }
                }

                band.Freeze();
                context.DrawGeometry(palette.AccentSoft, null, band);
                DrawLine(context, palette.AccentPen, segment.Select(column => new Point(ColumnX(column), Y(sums[column] / replies[column]))));
            }

            if (clipped)
            {
                for (var column = 0; column < columns; column++)
                {
                    if (slowest[column] > top)
                    {
                        context.DrawRectangle(palette.Text, null, new Rect(ColumnX(column) - 1, area.Top - 5, 2, 5));
                    }
                }
            }

            DrawTimeAxis(context, area, palette, from, report.To);
            DrawLegend(
                context,
                area,
                palette,
                [(palette.Accent, "Mean reply", true), (palette.AccentSoft, "Slowest reply", false), (palette.DangerSoft, "Lost pings", false), (palette.Danger, "Outage", false)]);
        });
    }

    private static ReportChart LossPerSlice(ConnectionReport report, Palette palette)
    {
        var buckets = report.Buckets;
        var worst = buckets.Max(bucket => bucket.LossFraction ?? 0);
        var (top, step) = ChartText.Axis(worst * 100, 5);
        var size = ReportText.BucketSize(report.BucketSize);
        var caption = $"The share of pings lost in each {size}. A green mark means the {size} was measured and lost nothing.";

        return Render($"Packet loss per {size}", caption, palette, (context, area) =>
        {
            DrawValueGrid(context, area, palette, top, step, value => string.Create(CultureInfo.CurrentCulture, $"{value:0.#} %"));
            var slotWidth = area.Width / buckets.Count;
            var barWidth = Math.Max(1, slotWidth * 0.72);
            for (var index = 0; index < buckets.Count; index++)
            {
                var loss = (buckets[index].LossFraction ?? 0) * 100;
                var left = area.Left + (index * slotWidth) + ((slotWidth - barWidth) / 2);
                if (loss <= 0)
                {
                    context.DrawRectangle(palette.Success, null, new Rect(left, area.Bottom - 2, barWidth, 2));
                    continue;
                }

                var height = Math.Max(2, area.Height * Math.Min(1, loss / top));
                context.DrawRectangle(palette.Danger, null, new Rect(left, area.Bottom - height, barWidth, height));
            }

            var withDate = report.From.Date != report.To.Date;
            var every = Math.Max(1, (int)Math.Ceiling(buckets.Count / 8.0));
            for (var index = 0; index < buckets.Count; index += every)
            {
                var start = buckets[index].Start;
                var label = Text(
                    withDate && report.BucketSize >= TimeSpan.FromDays(1) ? start.ToString("d MMM", CultureInfo.CurrentCulture)
                        : withDate ? start.ToString("d MMM HH:mm", CultureInfo.CurrentCulture)
                        : start.ToString("HH:mm", CultureInfo.CurrentCulture),
                    10,
                    palette.Muted,
                    Regular);
                var center = area.Left + ((index + 0.5) * slotWidth);
                context.DrawText(label, new Point(Math.Clamp(center - (label.Width / 2), area.Left - 20, area.Right - label.Width), area.Bottom + 7));
            }

            DrawOffsetNote(context, area, palette, report.From.Offset);
        });
    }

    private static ReportChart LatencySpread(ConnectionReport report, Palette palette)
    {
        var latency = report.Statistics.Latency!;
        var limit = Math.Max(latency.Percentile(99), latency.Minimum + 1);
        var (axisRight, _) = ChartText.Axis(limit, 10);
        var bins = 40;
        var width = axisRight / bins;
        var counts = new int[bins + 1];
        foreach (var sample in latency.Samples)
        {
            counts[sample >= axisRight ? bins : Math.Clamp((int)(sample / width), 0, bins - 1)]++;
        }

        var (top, step) = ChartText.Axis(counts.Max(), 4);
        var over = counts[bins];
        var caption = $"How many replies took how long, in steps of {Units.Milliseconds(width)}. Median {Units.Milliseconds(latency.Median)}, "
            + $"95 % within {Units.Milliseconds(latency.Percentile(95))}."
            + (over > 0 ? $" The last bar holds the {Units.Count(over)} replies of {Units.Milliseconds(axisRight)} or more." : string.Empty);

        return Render("How replies were spread", caption, palette, (context, area) =>
        {
            DrawValueGrid(context, area, palette, top, step, value => Units.Count((long)value));
            var slotWidth = area.Width / (bins + 1);
            for (var index = 0; index <= bins; index++)
            {
                if (counts[index] == 0)
                {
                    continue;
                }

                var height = Math.Max(1.5, area.Height * counts[index] / top);
                var brush = index == bins ? palette.Warning : palette.Accent;
                context.DrawRectangle(brush, null, new Rect(area.Left + (index * slotWidth) + 1, area.Bottom - height, Math.Max(1, slotWidth - 2), height));
            }

            double X(double value) => area.Left + (Math.Clamp(value / axisRight, 0, 1) * bins * slotWidth);
            foreach (var (value, label, brush) in new[] { (latency.Median, "median", palette.Text), (latency.Percentile(95), "95 %", palette.Warning) })
            {
                var x = X(value);
                context.DrawLine(palette.DashedPen(brush), new Point(x, area.Top), new Point(x, area.Bottom));
                var text = Text($"{label} {Units.Milliseconds(value)}", 10, brush, SemiBold);
                context.DrawText(text, new Point(Math.Min(x + 4, area.Right - text.Width), area.Top + (label == "median" ? 2 : 16)));
            }

            for (var tick = 0d; tick <= axisRight + (width / 2); tick += axisRight / 4)
            {
                var label = Text(Units.Milliseconds(tick).Replace(".0 ms", " ms", StringComparison.Ordinal), 10, palette.Muted, Regular);
                context.DrawText(label, new Point(Math.Clamp(X(tick) - (label.Width / 2), area.Left - 20, area.Right - label.Width), area.Bottom + 7));
            }
        });
    }

    private static ReportChart SpeedTests(ConnectionReport report, Palette palette)
    {
        var tests = report.SpeedTests.TakeLast(MaximumSpeedTests).ToList();
        var fastest = tests.Max(test => Math.Max(test.Download.AverageBitsPerSecond, test.Upload.AverageBitsPerSecond)) / 1e6;
        var (top, step) = ChartText.Axis(fastest, 10);
        var caption = "The average download and upload of each speed test in the period, in megabits per second."
            + (report.SpeedTests.Count > tests.Count ? $" The last {tests.Count} of {report.SpeedTests.Count} tests are shown." : string.Empty);

        return Render("Speed tests", caption, palette, (context, area) =>
        {
            DrawValueGrid(context, area, palette, top, step, value => string.Create(CultureInfo.CurrentCulture, $"{value:0} Mbps"));
            var slotWidth = area.Width / tests.Count;
            var barWidth = Math.Min(48, slotWidth * 0.34);
            var withDate = report.From.Date != report.To.Date;
            for (var index = 0; index < tests.Count; index++)
            {
                var test = tests[index];
                var center = area.Left + ((index + 0.5) * slotWidth);
                Bar(center - barWidth - 2, test.Download.AverageBitsPerSecond, palette.Accent);
                Bar(center + 2, test.Upload.AverageBitsPerSecond, palette.AccentMid);
                var started = test.StartedAt.ToOffset(report.From.Offset);
                var label = Text(started.ToString(withDate ? "d MMM HH:mm" : "HH:mm", CultureInfo.CurrentCulture), 10, palette.Muted, Regular);
                context.DrawText(label, new Point(center - (label.Width / 2), area.Bottom + 7));
            }

            DrawLegend(context, area, palette, [(palette.Accent, "Download", false), (palette.AccentMid, "Upload", false)]);

            void Bar(double left, double bitsPerSecond, Brush brush)
            {
                var height = Math.Max(1.5, area.Height * Math.Min(1, bitsPerSecond / 1e6 / top));
                context.DrawRectangle(brush, null, new Rect(left, area.Bottom - height, barWidth, height));
                var value = Text(Units.MegabitsNumber(bitsPerSecond), 10, palette.Text, SemiBold);
                context.DrawText(value, new Point(left + ((barWidth - value.Width) / 2), area.Bottom - height - value.Height - 1));
            }
        });
    }

    private static ReportChart Render(string title, string caption, Palette palette, Action<DrawingContext, Rect> draw)
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(palette.Background, null, new Rect(0, 0, Width, Height));
            draw(context, new Rect(64, 30, Width - 64 - 18, Height - 30 - 32));
        }

        var bitmap = new RenderTargetBitmap((int)(Width * Scale), (int)(Height * Scale), 96 * Scale, 96 * Scale, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var png = new MemoryStream();
        encoder.Save(png);
        return new ReportChart(title, caption, png.ToArray(), bitmap.PixelWidth, bitmap.PixelHeight);
    }

    private static void DrawValueGrid(DrawingContext context, Rect area, Palette palette, double top, double step, Func<double, string> format)
    {
        for (var value = 0d; value <= top + (step / 2); value += step)
        {
            var y = Math.Round(area.Bottom - (value / top * area.Height)) + 0.5;
            context.DrawLine(palette.GridPen, new Point(area.Left, y), new Point(area.Right, y));
            var label = Text(format(value), 10, palette.Muted, Regular);
            context.DrawText(label, new Point(area.Left - label.Width - 8, y - (label.Height / 2)));
        }
    }

    // Clock-aligned ticks in the measured offset, about six of them.
    private static void DrawTimeAxis(DrawingContext context, Rect area, Palette palette, DateTimeOffset from, DateTimeOffset to)
    {
        var span = to - from;
        if (span <= TimeSpan.Zero)
        {
            var only = Text(from.ToString("HH:mm:ss", CultureInfo.CurrentCulture), 10, palette.Muted, Regular);
            context.DrawText(only, new Point(area.Left + ((area.Width - only.Width) / 2), area.Bottom + 7));
            return;
        }

        var step = TickSteps.FirstOrDefault(candidate => span.Ticks / candidate.Ticks <= 6, TickSteps[^1]);
        var format = step >= TimeSpan.FromDays(1) ? "ddd d MMM"
            : span > TimeSpan.FromDays(1) ? "ddd HH:mm"
            : step < TimeSpan.FromMinutes(1) ? "HH:mm:ss"
            : "HH:mm";
        var first = new DateTimeOffset((from.DateTime.Ticks + step.Ticks - 1) / step.Ticks * step.Ticks, from.Offset);
        for (var tick = first; tick <= to; tick += step)
        {
            var x = area.Left + (area.Width * (tick - from).Ticks / span.Ticks);
            context.DrawLine(palette.GridPen, new Point(x, area.Bottom), new Point(x, area.Bottom + 4));
            var label = Text(tick.ToString(format, CultureInfo.CurrentCulture), 10, palette.Muted, Regular);
            context.DrawText(label, new Point(Math.Clamp(x - (label.Width / 2), area.Left - 30, area.Right - label.Width), area.Bottom + 7));
        }

        DrawOffsetNote(context, area, palette, from.Offset);
    }

    private static void DrawOffsetNote(DrawingContext context, Rect area, Palette palette, TimeSpan offset)
    {
        var note = Text($"Times in {ReportText.Offset(offset)}", 10, palette.Muted, Regular);
        context.DrawText(note, new Point(area.Right - note.Width, area.Top - 22));
    }

    private static void DrawLegend(DrawingContext context, Rect area, Palette palette, IReadOnlyList<(Brush Brush, string Label, bool IsLine)> entries)
    {
        var x = area.Left;
        var y = area.Top - 22;
        foreach (var (brush, label, isLine) in entries)
        {
            if (isLine)
            {
                context.DrawRectangle(brush, null, new Rect(x, y + 6, 14, 2.5));
            }
            else
            {
                context.DrawRectangle(brush, null, new Rect(x, y + 2, 11, 11));
            }

            var text = Text(label, 10.5, palette.Text, Regular);
            context.DrawText(text, new Point(x + 17, y));
            x += 17 + text.Width + 16;
        }
    }

    private static void DrawLine(DrawingContext context, Pen pen, IEnumerable<Point> points)
    {
        var list = points.ToList();
        if (list.Count == 1)
        {
            context.DrawEllipse(pen.Brush, null, list[0], 1.8, 1.8);
            return;
        }

        var geometry = new StreamGeometry();
        using (var figure = geometry.Open())
        {
            figure.BeginFigure(list[0], false, false);
            figure.PolyLineTo(list.Skip(1).ToList(), true, true);
        }

        geometry.Freeze();
        context.DrawGeometry(null, pen, geometry);
    }

    // Runs of consecutive columns that have data, so lines break where there were no replies.
    private static IEnumerable<List<int>> Segments(int columns, Func<int, bool> hasData)
    {
        var current = new List<int>();
        for (var column = 0; column < columns; column++)
        {
            if (hasData(column))
            {
                current.Add(column);
                continue;
            }

            if (current.Count > 0)
            {
                yield return current;
                current = [];
            }
        }

        if (current.Count > 0)
        {
            yield return current;
        }
    }

    private static FormattedText Text(string text, double size, Brush brush, Typeface typeface) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, size, brush, Scale);

    /// <summary>The light palette with the report's accent, frozen for off-thread-safe drawing.</summary>
    private sealed record Palette(
        Brush Background,
        Brush Text,
        Brush Muted,
        Pen GridPen,
        Brush Accent,
        Brush AccentMid,
        Brush AccentSoft,
        Pen AccentPen,
        Brush Danger,
        Brush DangerSoft,
        Brush Success,
        Brush Warning)
    {
        public static Palette For(string accentHex)
        {
            var neutral = NeutralPalette.Light;
            var accent = ColorConverter.ConvertFromString(accentHex) is Color parsed ? parsed : AccentPalette.All[0].OnLight;
            var accentBrush = Frozen(new SolidColorBrush(accent));
            return new Palette(
                Frozen(new SolidColorBrush(neutral.Surface)),
                Frozen(new SolidColorBrush(neutral.TextPrimary)),
                Frozen(new SolidColorBrush(neutral.TextSecondary)),
                Frozen(new Pen(Frozen(new SolidColorBrush(neutral.Border)), 1)),
                accentBrush,
                Frozen(new SolidColorBrush(Color.FromArgb(0x8C, accent.R, accent.G, accent.B))),
                Frozen(new SolidColorBrush(Color.FromArgb(0x33, accent.R, accent.G, accent.B))),
                Frozen(new Pen(accentBrush, 1.6) { LineJoin = PenLineJoin.Round }),
                Frozen(new SolidColorBrush(neutral.Danger)),
                Frozen(new SolidColorBrush(Color.FromArgb(0x40, neutral.Danger.R, neutral.Danger.G, neutral.Danger.B))),
                Frozen(new SolidColorBrush(neutral.Success)),
                Frozen(new SolidColorBrush(neutral.Warning)));
        }

        public Pen DashedPen(Brush brush) => Frozen(new Pen(brush, 1.2) { DashStyle = new DashStyle([4, 3], 0) });

        private static T Frozen<T>(T freezable)
            where T : Freezable
        {
            freezable.Freeze();
            return freezable;
        }
    }
}
