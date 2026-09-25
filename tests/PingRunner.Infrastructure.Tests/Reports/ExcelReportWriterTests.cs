using ClosedXML.Excel;
using PingRunner.Core.Reports;
using PingRunner.Infrastructure.Importing;
using PingRunner.Infrastructure.Reports;
using PingRunner.Infrastructure.Tests.Fakes;

namespace PingRunner.Infrastructure.Tests.Reports;

public sealed class ExcelReportWriterTests
{
    [Fact]
    public async Task Write_MakesASheetPerTable_WithRealNumbers()
    {
        var report = SampleReports.Report();
        using var output = new MemoryStream();

        await new ExcelReportWriter().WriteAsync(report, output, TestContext.Current.CancellationToken);

        output.Position = 0;
        using var workbook = new XLWorkbook(output);
        Assert.Equal(["Summary", "Over time", "Outages", "Speed tests", "Connection", "Pings"], workbook.Worksheets.Select(sheet => sheet.Name));

        var summary = workbook.Worksheet("Summary");
        Assert.Equal("Connection report", summary.Cell(1, 1).GetText());
        var loss = summary.Column(1).CellsUsed().Single(cell => cell.GetText() == "Packet loss").CellRight();
        Assert.Equal(XLDataType.Number, loss.DataType);
        Assert.Equal(report.Statistics.LossFraction!.Value, loss.GetDouble(), 6);
        Assert.Contains(summary.Column(1).CellsUsed(), cell => cell.GetText() == "• " + report.Findings[0]);
        Assert.Single(summary.Pictures);

        var overTime = workbook.Worksheet("Over time");
        Assert.Equal(report.Buckets.Count + 1, overTime.LastRowUsed()!.RowNumber());
        Assert.Equal(XLDataType.DateTime, overTime.Cell(2, 1).DataType);
        Assert.Equal(report.Buckets[0].Start.DateTime, overTime.Cell(2, 1).GetDateTime());

        Assert.Equal(report.Statistics.Outages.Count + 1, workbook.Worksheet("Outages").LastRowUsed()!.RowNumber());
        Assert.Equal("203.0.113.9", workbook.Worksheet("Connection").Column(2).CellsUsed().Last().GetText());
        Assert.Equal(report.Attempts.Count + 1, workbook.Worksheet("Pings").LastRowUsed()!.RowNumber());
    }

    [Fact]
    public async Task Write_ThenImport_GivesBackThePings()
    {
        var report = SampleReports.Report(seconds: 300);
        using var output = new MemoryStream();
        await new ExcelReportWriter().WriteAsync(report, output, TestContext.Current.CancellationToken);
        output.Position = 0;

        var imported = await new ExcelPingReader().ReadAsync(output, "report.xlsx", TestContext.Current.CancellationToken);

        Assert.Equal(report.Attempts, imported.Attempts);
        Assert.Equal(0, imported.SkippedRows);
    }

    [Fact]
    public async Task Write_WithoutPings_LeavesOutTheSheet()
    {
        var report = SampleReports.Report(options: new ReportOptions { IncludeAllPings = false, IncludeConnection = false, IncludeSpeedTests = false });
        using var output = new MemoryStream();

        await new ExcelReportWriter().WriteAsync(report, output, TestContext.Current.CancellationToken);

        output.Position = 0;
        using var workbook = new XLWorkbook(output);
        Assert.Equal(["Summary", "Over time", "Outages"], workbook.Worksheets.Select(sheet => sheet.Name));
    }

    [Fact]
    public async Task Write_NoOutages_SaysNone()
    {
        var report = SampleReports.Report(seconds: 60);
        using var output = new MemoryStream();

        await new ExcelReportWriter().WriteAsync(report, output, TestContext.Current.CancellationToken);

        output.Position = 0;
        using var workbook = new XLWorkbook(output);
        Assert.Empty(report.Statistics.Outages);
        Assert.Equal("None.", workbook.Worksheet("Outages").Cell(2, 1).GetText());
    }
}
