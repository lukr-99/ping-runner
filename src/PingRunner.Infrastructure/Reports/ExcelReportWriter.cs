using System.Globalization;
using ClosedXML.Excel;
using PingRunner.Core.Formatting;
using PingRunner.Core.Reports;

namespace PingRunner.Infrastructure.Reports;

/// <summary>
/// Writes a report as an Excel workbook for working with the data: a summary sheet with the key
/// figures as real numbers, the findings and the charts, then one sheet per table (over time,
/// outages, speed tests, connection) and every ping. Times are the clock times the pings were
/// measured at, with the offset alongside, so the Pings sheet imports back into Ping Runner as-is.
/// </summary>
public sealed class ExcelReportWriter : IReportWriter
{
    /// <summary>A sheet holds 1,048,576 rows; pings past this are left out and the summary says so.</summary>
    public const int MaximumPingRows = 1_000_000;

    private const string DateTimeFormat = "yyyy-mm-dd hh:mm:ss";
    private const string DurationFormat = "[h]:mm:ss";
    private const string MillisecondsFormat = "0.0";
    private const string CountFormat = "#,##0";
    private const string ShareFormat = "0.00%";
    private const int ChartWidthPixels = 760;

    public ReportFormat Format => ReportFormat.Excel;

    public string FileExtension => ".xlsx";

    public string FileFilter => "Excel workbook (*.xlsx)|*.xlsx";

    public async Task WriteAsync(ConnectionReport report, Stream output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(output);

        var bytes = await Task.Run(() => Render(report, cancellationToken), cancellationToken).ConfigureAwait(false);
        await output.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    private static byte[] Render(ConnectionReport report, CancellationToken cancellationToken)
    {
        var accent = XLColor.FromHtml(Rgb.ParseOrDefault(report.AccentColor).Hex);
        using var workbook = new XLWorkbook();
        workbook.Properties.Title = report.Title;
        workbook.Properties.Subject = $"{report.Target}, {report.PeriodText}";
        workbook.Properties.Author = "Ping Runner";
        workbook.Properties.Comments = $"Made with Ping Runner {report.AppVersion}";

        AddSummary(workbook, report, accent);
        AddOverTime(workbook, report, accent);
        AddOutages(workbook, report, accent);
        AddSpeedTests(workbook, report, accent);
        AddConnection(workbook, report, accent);
        if (report.IncludeAllPings)
        {
            AddPings(workbook, report, accent, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var buffer = new MemoryStream();
        workbook.SaveAs(buffer);
        return buffer.ToArray();
    }

    private static void AddSummary(XLWorkbook workbook, ConnectionReport report, XLColor accent)
    {
        var sheet = workbook.AddWorksheet("Summary");
        sheet.ShowGridLines = false;
        sheet.Column(1).Width = 30;
        sheet.Column(2).Width = 24;
        sheet.Column(3).Width = 4;

        var title = sheet.Cell(1, 1);
        title.Value = report.Title;
        title.Style.Font.FontSize = 18;
        title.Style.Font.Bold = true;
        title.Style.Font.FontColor = accent;
        sheet.Row(1).Height = 28;

        var generated = report.GeneratedAt.ToOffset(report.From.Offset);
        Muted(sheet.Cell(2, 1), $"{report.Target}  ·  {report.PeriodText}");
        Muted(sheet.Cell(3, 1), $"{report.Subject} · made {ReportText.Stamp(generated)} ({ReportText.Offset(generated.Offset)}) with Ping Runner {report.AppVersion}");

        var row = 5;
        if (report.Notes.Length > 0)
        {
            Label(sheet.Cell(row, 1), "Notes");
            sheet.Cell(row, 2).Value = report.Notes;
            row += 2;
        }

        row = Heading(sheet, row, "Key figures", accent);
        var statistics = report.Statistics;
        row = Figure(sheet, row, "First ping", report.From.DateTime, DateTimeFormat);
        row = Figure(sheet, row, "Last ping", report.To.DateTime, DateTimeFormat);
        row = Figure(sheet, row, "Time zone", ReportText.Offset(report.From.Offset));
        row = Figure(sheet, row, "Target", report.Target);
        row = Figure(sheet, row, "Pings sent", statistics.Sent, CountFormat);
        row = Figure(sheet, row, "Answered", statistics.Received, CountFormat);
        row = Figure(sheet, row, "Lost", statistics.Lost, CountFormat);
        row = Figure(sheet, row, "Packet loss", statistics.LossFraction, ShareFormat);
        row = Figure(sheet, row, "Outages", statistics.Outages.Count, CountFormat);
        row = Figure(sheet, row, "Longest outage", statistics.LongestOutage?.Duration, DurationFormat);
        row = Figure(sheet, row, "Time in outages", TimeSpan.FromTicks(statistics.Outages.Sum(outage => outage.Duration.Ticks)), DurationFormat);
        row = Figure(sheet, row, "Minimum latency (ms)", statistics.Latency?.Minimum, MillisecondsFormat);
        row = Figure(sheet, row, "Median latency (ms)", statistics.Latency?.Median, MillisecondsFormat);
        row = Figure(sheet, row, "Mean latency (ms)", statistics.Latency?.Mean, MillisecondsFormat);
        row = Figure(sheet, row, "95th percentile (ms)", statistics.Latency?.Percentile(95), MillisecondsFormat);
        row = Figure(sheet, row, "Slowest (ms)", statistics.Latency?.Maximum, MillisecondsFormat);
        row = Figure(sheet, row, "Jitter (ms)", statistics.JitterMilliseconds, MillisecondsFormat);
        row = Figure(sheet, row, "Call quality (MOS, 1–4.5)", statistics.CallQuality?.MeanOpinionScore, "0.0");
        if (report.SpeedTests.Count > 0)
        {
            row = Figure(sheet, row, "Speed tests", report.SpeedTests.Count, CountFormat);
            row = Figure(sheet, row, "Download, average (Mbps)", report.SpeedTests.Average(test => test.Download.AverageBitsPerSecond) / 1e6, "0.0");
            row = Figure(sheet, row, "Upload, average (Mbps)", report.SpeedTests.Average(test => test.Upload.AverageBitsPerSecond) / 1e6, "0.0");
            row = Figure(sheet, row, "Download, best (Mbps)", report.SpeedTests.Max(test => test.Download.PeakBitsPerSecond) / 1e6, "0.0");
            row = Figure(sheet, row, "Upload, best (Mbps)", report.SpeedTests.Max(test => test.Upload.PeakBitsPerSecond) / 1e6, "0.0");
        }

        if (report.IncludeAllPings && report.Attempts.Count > MaximumPingRows)
        {
            row++;
            Muted(sheet.Cell(row++, 1), $"The Pings sheet holds the first {Units.Count(MaximumPingRows)} of {Units.Count(report.Attempts.Count)} pings, as many as a sheet can take.");
        }

        row = Heading(sheet, row + 1, "Findings", accent);
        foreach (var finding in report.Findings)
        {
            sheet.Cell(row++, 1).Value = "• " + finding;
        }

        foreach (var chart in report.Charts)
        {
            row = Heading(sheet, row + 1, chart.Title, accent);
            using var png = new MemoryStream(chart.Png);
            var height = (int)Math.Round(ChartWidthPixels / (chart.AspectRatio > 0 ? chart.AspectRatio : 2.5));
            sheet.AddPicture(png).MoveTo(sheet.Cell(row, 1)).WithSize(ChartWidthPixels, height);
            row += (int)Math.Ceiling(height / 20.0) + 1;
            Muted(sheet.Cell(row++, 1), chart.Caption);
        }

        row = Heading(sheet, row + 1, "How this was measured", accent);
        foreach (var line in report.Method)
        {
            Muted(sheet.Cell(row++, 1), line);
        }

        sheet.SetTabColor(accent);
    }

    private static void AddOverTime(XLWorkbook workbook, ConnectionReport report, XLColor accent)
    {
        var offset = ReportText.Offset(report.From.Offset);
        AddDataSheet(
            workbook,
            "Over time",
            accent,
            [
                ($"Start ({offset})", DateTimeFormat, 20),
                ($"End ({offset})", DateTimeFormat, 20),
                ("Pings", CountFormat, 10),
                ("Answered", CountFormat, 11),
                ("Lost", CountFormat, 9),
                ("Loss", ShareFormat, 9),
                ("Mean (ms)", MillisecondsFormat, 11),
                ("95th percentile (ms)", MillisecondsFormat, 19),
                ("Slowest (ms)", MillisecondsFormat, 13),
                ("Outages started", CountFormat, 15),
            ],
            report.Buckets.Count,
            report.Buckets.Select(bucket => new object?[]
            {
                bucket.Start.DateTime,
                bucket.End.DateTime,
                bucket.Sent,
                bucket.Received,
                bucket.Lost,
                bucket.LossFraction,
                bucket.MeanMilliseconds,
                bucket.P95Milliseconds,
                bucket.MaximumMilliseconds,
                bucket.OutagesStarted,
            }),
            CancellationToken.None);
    }

    private static void AddOutages(XLWorkbook workbook, ConnectionReport report, XLColor accent)
    {
        var offset = ReportText.Offset(report.From.Offset);
        AddDataSheet(
            workbook,
            "Outages",
            accent,
            [
                ($"Started ({offset})", DateTimeFormat, 20),
                ($"Ended ({offset})", DateTimeFormat, 20),
                ("Duration", DurationFormat, 11),
                ("Lost pings", CountFormat, 11),
                ("Still going at the end", null, 21),
            ],
            report.Statistics.Outages.Count,
            report.Statistics.Outages.Select(outage => new object?[]
            {
                outage.Start.DateTime,
                outage.End.DateTime,
                outage.Duration,
                outage.LostCount,
                outage.IsOngoing ? "yes" : null,
            }),
            CancellationToken.None);
    }

    private static void AddSpeedTests(XLWorkbook workbook, ConnectionReport report, XLColor accent)
    {
        if (report.SpeedTests.Count == 0)
        {
            return;
        }

        var offset = ReportText.Offset(report.From.Offset);
        AddDataSheet(
            workbook,
            "Speed tests",
            accent,
            [
                ($"Started ({offset})", DateTimeFormat, 20),
                ("Server", null, 18),
                ("Download (Mbps)", "0.0", 16),
                ("Upload (Mbps)", "0.0", 14),
                ("Best download (Mbps)", "0.0", 20),
                ("Best upload (Mbps)", "0.0", 18),
                ("Idle latency (ms)", MillisecondsFormat, 16),
                ("Latency downloading (ms)", MillisecondsFormat, 23),
                ("Latency uploading (ms)", MillisecondsFormat, 21),
                ("Bufferbloat (ms)", MillisecondsFormat, 16),
                ("Grade", null, 8),
                ("Data used (MB)", "0.0", 14),
            ],
            report.SpeedTests.Count,
            report.SpeedTests.Select(test => new object?[]
            {
                test.StartedAt.ToOffset(report.From.Offset).DateTime,
                test.Server,
                test.Download.AverageBitsPerSecond / 1e6,
                test.Upload.AverageBitsPerSecond / 1e6,
                test.Download.PeakBitsPerSecond / 1e6,
                test.Upload.PeakBitsPerSecond / 1e6,
                test.IdleLatency?.Median,
                test.DownloadLatency?.Median,
                test.UploadLatency?.Median,
                test.Bufferbloat?.IncreaseMilliseconds,
                test.Bufferbloat?.GradeText,
                test.TotalBytes / 1e6,
            }),
            CancellationToken.None);
    }

    private static void AddConnection(XLWorkbook workbook, ConnectionReport report, XLColor accent)
    {
        if (report.Connection is null && report.PublicIp is null)
        {
            return;
        }

        var details = new List<object?[]>();
        if (report.Connection is { } connection)
        {
            details.Add(["Adapter", connection.AdapterName]);
            details.Add(["Description", connection.AdapterDescription]);
            details.Add(["Type", connection.AdapterKind]);
            details.Add(["Link speed", Units.LinkSpeed(connection.LinkSpeedBitsPerSecond)]);
            details.Add(["Local addresses", string.Join(", ", connection.LocalAddresses)]);
            details.Add(["Gateways", string.Join(", ", connection.Gateways)]);
            details.Add(["DNS servers", string.Join(", ", connection.DnsServers)]);
        }

        if (report.PublicIp is { } publicIp)
        {
            details.Add(["Public IP address", publicIp]);
        }

        var sheet = AddDataSheet(workbook, "Connection", accent, [("Detail", null, 20), ("Value", null, 60)], details.Count, details, CancellationToken.None);
        Muted(sheet.Cell(details.Count + 3, 1), "Recorded when the report was made, which may be after the pings.");
    }

    private static void AddPings(XLWorkbook workbook, ConnectionReport report, XLColor accent, CancellationToken cancellationToken)
    {
        var count = Math.Min(report.Attempts.Count, MaximumPingRows);
        AddDataSheet(
            workbook,
            "Pings",
            accent,
            [
                ("Time", DateTimeFormat, 20),
                ("UTC offset", null, 11),
                ("Target", null, 18),
                ("Result", null, 9),
                ("Latency (ms)", "0", 13),
                ("Details", null, 40),
            ],
            count,
            report.Attempts.Take(count).Select(attempt => new object?[]
            {
                attempt.Timestamp.DateTime,
                ReportText.Offset(attempt.Timestamp.Offset),
                attempt.TargetHost,
                attempt.IsSuccess ? "Reply" : "Lost",
                attempt.RoundtripMilliseconds,
                attempt.Details,
            }),
            cancellationToken);
    }

    private static IXLWorksheet AddDataSheet(
        XLWorkbook workbook,
        string name,
        XLColor accent,
        IReadOnlyList<(string Header, string? Format, double Width)> columns,
        int count,
        IEnumerable<object?[]> rows,
        CancellationToken cancellationToken)
    {
        var sheet = workbook.AddWorksheet(name);
        for (var index = 0; index < columns.Count; index++)
        {
            var (header, format, width) = columns[index];
            var column = sheet.Column(index + 1);
            column.Width = width;
            if (format is not null)
            {
                column.Style.NumberFormat.Format = format;
            }

            sheet.Cell(1, index + 1).Value = header;
        }

        var headerRow = sheet.Range(1, 1, 1, columns.Count);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Font.FontColor = XLColor.White;
        headerRow.Style.Fill.BackgroundColor = accent;
        sheet.SheetView.FreezeRows(1);

        if (count == 0)
        {
            Muted(sheet.Cell(2, 1), "None.");
            return sheet;
        }

        sheet.Cell(2, 1).InsertData(rows.Select((row, index) =>
        {
            if (index % 10_000 == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            return row;
        }));

        var table = sheet.Range(1, 1, count + 1, columns.Count).CreateTable(string.Concat(name.Where(char.IsLetter)));
        table.Theme = XLTableTheme.TableStyleLight1;
        headerRow.Style.Fill.BackgroundColor = accent;
        headerRow.Style.Font.FontColor = XLColor.White;
        return sheet;
    }

    private static int Heading(IXLWorksheet sheet, int row, string text, XLColor accent)
    {
        var cell = sheet.Cell(row, 1);
        cell.Value = text;
        cell.Style.Font.Bold = true;
        cell.Style.Font.FontSize = 12;
        cell.Style.Font.FontColor = accent;
        sheet.Range(row, 1, row, 2).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        sheet.Range(row, 1, row, 2).Style.Border.BottomBorderColor = accent;
        return row + 1;
    }

    private static int Figure(IXLWorksheet sheet, int row, string label, object? value, string? format = null)
    {
        Label(sheet.Cell(row, 1), label);
        var cell = sheet.Cell(row, 2);
        cell.Value = value switch
        {
            null => Units.None,
            DateTime time => time,
            TimeSpan span => span,
            int number => number,
            double number => number,
            _ => Convert.ToString(value, CultureInfo.CurrentCulture),
        };
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        if (value is not null && format is not null)
        {
            cell.Style.NumberFormat.Format = format;
        }

        return row + 1;
    }

    private static void Label(IXLCell cell, string text)
    {
        cell.Value = text;
        cell.Style.Font.FontColor = XLColor.FromHtml("#475467");
    }

    private static void Muted(IXLCell cell, string text)
    {
        cell.Value = text;
        cell.Style.Font.FontColor = XLColor.FromHtml("#475467");
        cell.Style.Font.FontSize = 9;
    }
}
