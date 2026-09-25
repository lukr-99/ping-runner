using System.Globalization;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PingRunner.Core.Formatting;
using PingRunner.Core.Reports;
using PingRunner.Core.SpeedTest;
using PingRunner.Core.Statistics;

namespace PingRunner.Infrastructure.Reports;

/// <summary>
/// Writes a report as an A4 PDF meant to be handed to someone: key figures, findings, charts, the
/// breakdown over time, outages, speed tests and connection details, and how it was all measured.
/// Individual pings stay in the Excel version. Segoe UI is embedded, so it prints the same anywhere.
/// </summary>
public sealed class PdfReportWriter : IReportWriter
{
    public const int MaximumListedOutages = 200;

    private const double ContentCentimeters = 17.4;

    private static readonly Rgb Ink = new(0x10, 0x18, 0x28);
    private static readonly Rgb Muted = new(0x47, 0x54, 0x67);
    private static readonly Rgb Rule = new(0xEA, 0xEC, 0xF0);
    private static readonly Rgb Zebra = new(0xF9, 0xFA, 0xFB);
    private static readonly Rgb Good = new(0x06, 0x76, 0x47);
    private static readonly Rgb Warning = new(0xB5, 0x47, 0x08);
    private static readonly Rgb Bad = new(0xB4, 0x23, 0x18);

    public ReportFormat Format => ReportFormat.Pdf;

    public string FileExtension => ".pdf";

    public string FileFilter => "PDF document (*.pdf)|*.pdf";

    public async Task WriteAsync(ConnectionReport report, Stream output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(output);

        var bytes = await Task.Run(() => Render(report), cancellationToken).ConfigureAwait(false);
        await output.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    private static byte[] Render(ConnectionReport report)
    {
        PdfFonts.EnsureInstalled();
        var renderer = new PdfDocumentRenderer { Document = Compose(report) };
        renderer.RenderDocument();
        renderer.PdfDocument.Info.Creator = $"Ping Runner {report.AppVersion}";

        using var buffer = new MemoryStream();
        renderer.PdfDocument.Save(buffer, closeStream: false);
        return buffer.ToArray();
    }

    private static Document Compose(ConnectionReport report)
    {
        var accent = Rgb.ParseOrDefault(report.AccentColor);
        var document = new Document();
        document.Info.Title = report.Title;
        document.Info.Subject = $"{report.Target}, {report.PeriodText}";
        document.Info.Author = "Ping Runner";

        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = PdfFonts.Family;
        normal.Font.Size = 9.5;
        normal.Font.Color = Color(Ink);
        normal.ParagraphFormat.SpaceAfter = Unit.FromPoint(3);

        var heading = document.Styles[StyleNames.Heading1]!;
        heading.Font.Size = 13;
        heading.Font.Bold = true;
        heading.Font.Color = Color(accent);
        heading.ParagraphFormat.SpaceBefore = Unit.FromPoint(16);
        heading.ParagraphFormat.SpaceAfter = Unit.FromPoint(6);
        heading.ParagraphFormat.KeepWithNext = true;

        var section = document.AddSection();
        section.PageSetup = document.DefaultPageSetup.Clone();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.TopMargin = Unit.FromCentimeter(1.8);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(2);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.8);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.8);
        section.PageSetup.FooterDistance = Unit.FromCentimeter(0.9);

        AddFooter(section, report);
        AddTitle(section, report, accent);
        AddKeyFigures(section, report, accent);
        AddFindings(section, report, accent);
        AddCharts(section, report);
        AddOverTime(section, report, accent);
        AddOutages(section, report, accent);
        AddSpeedTests(section, report, accent);
        AddConnection(section, report);
        AddMethod(section, report);
        return document;
    }

    private static void AddFooter(Section section, ConnectionReport report)
    {
        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Font.Size = 7.5;
        footer.Format.Font.Color = Color(Muted);
        footer.Format.TabStops.AddTabStop(Unit.FromCentimeter(ContentCentimeters), TabAlignment.Right);
        footer.AddText($"{report.Title} · {report.Target} · {report.PeriodText}");
        footer.AddTab();
        footer.AddText("Page ");
        footer.AddPageField();
        footer.AddText(" of ");
        footer.AddNumPagesField();
    }

    private static void AddTitle(Section section, ConnectionReport report, Rgb accent)
    {
        var title = section.AddParagraph(report.Title);
        title.Format.Font.Size = 22;
        title.Format.Font.Bold = true;
        title.Format.Font.Color = Color(accent);
        title.Format.SpaceAfter = Unit.FromPoint(2);

        var subtitle = section.AddParagraph($"{report.Target}  ·  {report.PeriodText}");
        subtitle.Format.Font.Size = 10.5;
        subtitle.Format.Font.Color = Color(Muted);

        var generated = report.GeneratedAt.ToOffset(report.From.Offset);
        var meta = section.AddParagraph(
            $"{report.Subject} · made {ReportText.Stamp(generated)} ({ReportText.Offset(generated.Offset)}) with Ping Runner {report.AppVersion}");
        meta.Format.Font.Size = 8.5;
        meta.Format.Font.Color = Color(Muted);
        meta.Format.Borders.Bottom.Width = 1.5;
        meta.Format.Borders.Bottom.Color = Color(accent);
        meta.Format.Borders.DistanceFromBottom = Unit.FromPoint(8);
        meta.Format.SpaceAfter = Unit.FromPoint(10);

        if (report.Notes.Length == 0)
        {
            return;
        }

        var notes = section.AddParagraph();
        notes.Format.Shading.Color = Color(accent.Tint(0.92));
        notes.Format.Borders.Left.Width = 3;
        notes.Format.Borders.Left.Color = Color(accent);
        notes.Format.Borders.DistanceFromLeft = Unit.FromPoint(8);
        notes.Format.Borders.DistanceFromTop = Unit.FromPoint(5);
        notes.Format.Borders.DistanceFromBottom = Unit.FromPoint(5);
        notes.Format.LeftIndent = Unit.FromPoint(11);
        notes.Format.SpaceAfter = Unit.FromPoint(10);
        var lines = report.Notes.ReplaceLineEndings("\n").Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            if (index > 0)
            {
                notes.AddLineBreak();
            }

            notes.AddText(lines[index]);
        }
    }

    private static void AddKeyFigures(Section section, ConnectionReport report, Rgb accent)
    {
        var statistics = report.Statistics;
        var loss = statistics.LossFraction ?? 0;
        var outageColor = statistics.Outages.Count == 0 ? Good : Bad;
        var quality = statistics.CallQuality;
        var tiles = new List<(string Label, string Value, Rgb Color)>
        {
            ("Pings sent", Units.Count(statistics.Sent), accent),
            ("Packet loss", Units.Percent(statistics.LossFraction), loss == 0 ? Good : loss <= 0.01 ? Warning : Bad),
            ("Outages", Units.Count(statistics.Outages.Count), outageColor),
            ("Longest outage", statistics.LongestOutage is { } longest ? Units.Span(longest.Duration) : "None", outageColor),
            ("Median latency", Units.Milliseconds(statistics.Latency?.Median), accent),
            ("95th percentile", Units.Milliseconds(statistics.Latency?.Percentile(95)), accent),
            ("Jitter", Units.Milliseconds(statistics.JitterMilliseconds), accent),
            (
                "Call quality (MOS)",
                quality is null ? Units.None : string.Create(CultureInfo.CurrentCulture, $"{quality.MeanOpinionScore:0.0} · {quality.Rating.ToString().ToLowerInvariant()}"),
                quality?.Rating switch
                {
                    null => accent,
                    >= CallQualityRating.Good => Good,
                    CallQualityRating.Fair => Warning,
                    _ => Bad,
                }),
        };

        if (report.SpeedTests.Count > 0)
        {
            var bloat = report.SpeedTests[^1].Bufferbloat;
            tiles.Add(("Download, average", Units.BitsPerSecond(report.SpeedTests.Average(test => test.Download.AverageBitsPerSecond)), accent));
            tiles.Add(("Upload, average", Units.BitsPerSecond(report.SpeedTests.Average(test => test.Upload.AverageBitsPerSecond)), accent));
            tiles.Add(("Download, best", Units.BitsPerSecond(report.SpeedTests.Max(test => test.Download.PeakBitsPerSecond)), accent));
            tiles.Add((
                "Bufferbloat",
                bloat is null ? Units.None : $"{bloat.GradeText} · +{Units.Milliseconds(bloat.IncreaseMilliseconds)}",
                bloat?.Grade switch
                {
                    null => accent,
                    <= BufferbloatGrade.A => Good,
                    <= BufferbloatGrade.C => Warning,
                    _ => Bad,
                }));
        }

        var table = section.AddTable();
        table.Borders.Width = 4;
        table.Borders.Color = Colors.White;
        for (var column = 0; column < 4; column++)
        {
            table.AddColumn(Unit.FromCentimeter(ContentCentimeters / 4));
        }

        Row? row = null;
        for (var index = 0; index < tiles.Count; index++)
        {
            if (index % 4 == 0)
            {
                row = table.AddRow();
            }

            var (label, value, color) = tiles[index];
            var cell = row!.Cells[index % 4];
            cell.Shading.Color = Color(accent.Tint(0.93));
            var figure = cell.AddParagraph(value);
            figure.Format.Font.Size = 14;
            figure.Format.Font.Bold = true;
            figure.Format.Font.Color = Color(color);
            figure.Format.SpaceBefore = Unit.FromPoint(5);
            figure.Format.LeftIndent = Unit.FromPoint(5);
            figure.Format.SpaceAfter = Unit.FromPoint(0);
            var caption = cell.AddParagraph(label);
            caption.Format.Font.Size = 8;
            caption.Format.Font.Color = Color(Muted);
            caption.Format.LeftIndent = Unit.FromPoint(5);
            caption.Format.SpaceAfter = Unit.FromPoint(5);
        }
    }

    private static void AddFindings(Section section, ConnectionReport report, Rgb accent)
    {
        section.AddParagraph("Findings", StyleNames.Heading1);
        foreach (var finding in report.Findings)
        {
            var paragraph = section.AddParagraph();
            paragraph.Format.LeftIndent = Unit.FromCentimeter(0.45);
            paragraph.Format.FirstLineIndent = Unit.FromCentimeter(-0.45);
            paragraph.Format.TabStops.AddTabStop(Unit.FromCentimeter(0.45));
            paragraph.Format.SpaceAfter = Unit.FromPoint(4);
            var bullet = paragraph.AddFormattedText("•");
            bullet.Color = Color(accent);
            bullet.Bold = true;
            paragraph.AddTab();
            paragraph.AddText(finding);
        }
    }

    private static void AddCharts(Section section, ConnectionReport report)
    {
        if (report.Charts.Count == 0)
        {
            return;
        }

        section.AddParagraph("Charts", StyleNames.Heading1);
        foreach (var chart in report.Charts)
        {
            var title = section.AddParagraph(chart.Title);
            title.Format.Font.Bold = true;
            title.Format.Font.Size = 10.5;
            title.Format.SpaceBefore = Unit.FromPoint(6);
            title.Format.KeepWithNext = true;

            var holder = section.AddParagraph();
            holder.Format.KeepWithNext = true;
            holder.Format.SpaceAfter = Unit.FromPoint(1);
            var image = holder.AddImage("base64:" + Convert.ToBase64String(chart.Png));
            image.LockAspectRatio = true;
            image.Width = Unit.FromCentimeter(ContentCentimeters);

            var caption = section.AddParagraph(chart.Caption);
            caption.Format.Font.Size = 8;
            caption.Format.Font.Color = Color(Muted);
            caption.Format.SpaceAfter = Unit.FromPoint(8);
        }
    }

    private static void AddOverTime(Section section, ConnectionReport report, Rgb accent)
    {
        section.AddParagraph($"Over time, per {ReportText.BucketSize(report.BucketSize)}", StyleNames.Heading1);
        var withDate = report.From.Date != report.To.Date;
        var table = AddTable(
            section,
            accent,
            ("Time", 3.9, false),
            ("Pings", 1.7, true),
            ("Lost", 1.6, true),
            ("Loss", 1.7, true),
            ("Mean", 2.1, true),
            ("95th pct.", 2.1, true),
            ("Slowest", 2.1, true),
            ("Outages", 2.2, true));

        foreach (var bucket in report.Buckets)
        {
            var row = AddRow(
                table,
                ReportText.Bucket(bucket, withDate),
                Units.Count(bucket.Sent),
                Units.Count(bucket.Lost),
                Units.Percent(bucket.LossFraction),
                Units.Milliseconds(bucket.MeanMilliseconds),
                Units.Milliseconds(bucket.P95Milliseconds),
                Units.Milliseconds(bucket.MaximumMilliseconds),
                bucket.OutagesStarted == 0 ? Units.None : Units.Count(bucket.OutagesStarted));
            if (bucket.Lost > 0)
            {
                Emphasize(row, Bad, 2, 3);
            }

            if (bucket.OutagesStarted > 0)
            {
                Emphasize(row, Bad, 7);
            }
        }
    }

    private static void AddOutages(Section section, ConnectionReport report, Rgb accent)
    {
        var outages = report.Statistics.Outages;
        section.AddParagraph("Outages", StyleNames.Heading1);
        if (outages.Count == 0)
        {
            section.AddParagraph($"None: the target never missed {PingStatistics.MinimumLostInARow} or more pings in a row.");
            return;
        }

        var withDate = report.From.Date != report.To.Date;
        string When(DateTimeOffset value) =>
            withDate ? value.ToString("ddd d MMM, HH:mm:ss", CultureInfo.CurrentCulture) : ReportText.Time(value);

        var table = AddTable(
            section,
            accent,
            ("#", 1.0, true),
            ("Started", 4.6, false),
            ("Ended", 4.6, false),
            ("Duration", 3.6, true),
            ("Lost pings", 3.6, true));
        foreach (var (outage, index) in outages.Take(MaximumListedOutages).Select((outage, index) => (outage, index)))
        {
            AddRow(
                table,
                Units.Count(index + 1),
                When(outage.Start),
                outage.IsOngoing ? $"{When(outage.End)} (still going)" : When(outage.End),
                Units.Span(outage.Duration),
                Units.Count(outage.LostCount));
        }

        if (outages.Count > MaximumListedOutages)
        {
            var more = section.AddParagraph(
                $"…and {Units.CountOf(outages.Count - MaximumListedOutages, "more outage", "more outages")}; the Excel version lists them all.");
            more.Format.Font.Color = Color(Muted);
            more.Format.SpaceBefore = Unit.FromPoint(4);
        }
    }

    private static void AddSpeedTests(Section section, ConnectionReport report, Rgb accent)
    {
        if (report.SpeedTests.Count == 0)
        {
            return;
        }

        section.AddParagraph("Speed tests", StyleNames.Heading1);
        var withDate = report.From.Date != report.To.Date;
        var table = AddTable(
            section,
            accent,
            ("Started", 2.8, false),
            ("Download", 2.3, true),
            ("Upload", 2.3, true),
            ("Best down", 2.3, true),
            ("Best up", 2.3, true),
            ("Idle latency", 2.3, true),
            ("Bufferbloat", 3.1, true));
        foreach (var test in report.SpeedTests)
        {
            var started = test.StartedAt.ToOffset(report.From.Offset);
            AddRow(
                table,
                withDate ? started.ToString("d MMM, HH:mm", CultureInfo.CurrentCulture) : started.ToString("HH:mm:ss", CultureInfo.CurrentCulture),
                Units.BitsPerSecond(test.Download.AverageBitsPerSecond),
                Units.BitsPerSecond(test.Upload.AverageBitsPerSecond),
                Units.BitsPerSecond(test.Download.PeakBitsPerSecond),
                Units.BitsPerSecond(test.Upload.PeakBitsPerSecond),
                Units.Milliseconds(test.IdleLatency?.Median),
                test.Bufferbloat is { } bloat ? $"{bloat.GradeText} (+{Units.Milliseconds(bloat.IncreaseMilliseconds)})" : Units.None);
        }
    }

    private static void AddConnection(Section section, ConnectionReport report)
    {
        if (report.Connection is null && report.PublicIp is null)
        {
            return;
        }

        section.AddParagraph("Connection", StyleNames.Heading1);
        var details = new List<(string Label, string Value)>();
        if (report.Connection is { } connection)
        {
            details.Add(("Adapter", $"{connection.AdapterName} ({connection.AdapterDescription})"));
            details.Add(("Type", connection.AdapterKind));
            details.Add(("Link speed", Units.LinkSpeed(connection.LinkSpeedBitsPerSecond)));
            details.Add(("Local addresses", Joined(connection.LocalAddresses)));
            details.Add(("Gateways", Joined(connection.Gateways)));
            details.Add(("DNS servers", Joined(connection.DnsServers)));
        }

        if (report.PublicIp is { } publicIp)
        {
            details.Add(("Public IP address", publicIp));
        }

        var table = section.AddTable();
        table.Borders.Visible = false;
        table.AddColumn(Unit.FromCentimeter(4.4));
        table.AddColumn(Unit.FromCentimeter(ContentCentimeters - 4.4));
        foreach (var (label, value) in details)
        {
            var row = table.AddRow();
            row.Borders.Bottom.Width = 0.5;
            row.Borders.Bottom.Color = Color(Rule);
            row.TopPadding = Unit.FromPoint(2);
            row.BottomPadding = Unit.FromPoint(2);
            row.Cells[0].AddParagraph(label).Format.Font.Color = Color(Muted);
            row.Cells[1].AddParagraph(value);
        }

        var note = section.AddParagraph("Recorded when the report was made, which may be after the pings.");
        note.Format.Font.Size = 8;
        note.Format.Font.Color = Color(Muted);
        note.Format.SpaceBefore = Unit.FromPoint(4);

        static string Joined(IReadOnlyList<string> values) => values.Count == 0 ? Units.None : string.Join(", ", values);
    }

    private static void AddMethod(Section section, ConnectionReport report)
    {
        section.AddParagraph("How this was measured", StyleNames.Heading1);
        foreach (var line in report.Method.Append("Every ping is listed in the Excel version of this report."))
        {
            var paragraph = section.AddParagraph(line);
            paragraph.Format.Font.Size = 8.5;
            paragraph.Format.Font.Color = Color(Muted);
        }
    }

    private static Table AddTable(Section section, Rgb accent, params (string Header, double Centimeters, bool Numeric)[] columns)
    {
        var table = section.AddTable();
        table.Borders.Visible = false;
        table.Format.Font.Size = 8.5;
        table.TopPadding = Unit.FromPoint(2.5);
        table.BottomPadding = Unit.FromPoint(2.5);
        foreach (var (_, centimeters, numeric) in columns)
        {
            var column = table.AddColumn(Unit.FromCentimeter(centimeters));
            column.Format.Alignment = numeric ? ParagraphAlignment.Right : ParagraphAlignment.Left;
        }

        var header = table.AddRow();
        header.HeadingFormat = true;
        header.Shading.Color = Color(accent);
        header.Format.Font.Bold = true;
        header.Format.Font.Color = Colors.White;
        for (var index = 0; index < columns.Length; index++)
        {
            header.Cells[index].AddParagraph(columns[index].Header);
        }

        return table;
    }

    private static Row AddRow(Table table, params string[] values)
    {
        var row = table.AddRow();
        if (table.Rows.Count % 2 == 1)
        {
            row.Shading.Color = Color(Zebra);
        }

        row.Borders.Bottom.Width = 0.5;
        row.Borders.Bottom.Color = Color(Rule);
        for (var index = 0; index < values.Length; index++)
        {
            row.Cells[index].AddParagraph(values[index]);
        }

        return row;
    }

    private static void Emphasize(Row row, Rgb color, params int[] cells)
    {
        foreach (var index in cells)
        {
            row.Cells[index].Format.Font.Color = Color(color);
            row.Cells[index].Format.Font.Bold = true;
        }
    }

    private static Color Color(Rgb rgb) => new(rgb.R, rgb.G, rgb.B);
}
