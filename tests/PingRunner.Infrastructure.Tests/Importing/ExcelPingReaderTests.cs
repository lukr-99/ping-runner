using ClosedXML.Excel;
using PingRunner.Core.Importing;
using PingRunner.Infrastructure.Importing;

namespace PingRunner.Infrastructure.Tests.Importing;

public sealed class ExcelPingReaderTests
{
    [Fact]
    public async Task Read_ForeignSheet_FindsTheHeaderBelowATitle()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Log");
        sheet.Cell(1, 1).Value = "Router ping log";
        Row(sheet, 3, "When", "Host", "RTT ms", "Status");
        Row(sheet, 4, new DateTime(2026, 9, 25, 9, 0, 0), "1.1.1.1", 14.6, "OK");
        Row(sheet, 5, new DateTime(2026, 9, 25, 9, 0, 5), "1.1.1.1", Blank.Value, "timeout");
        Row(sheet, 6, "yesterday", "1.1.1.1", 12, "OK");
        Row(sheet, 7, "2026-09-25T09:00:10+00:00", "1.1.1.1", "13", "true");

        var import = await Read(workbook, "router.xlsx");

        Assert.Equal(3, import.Attempts.Count);
        Assert.Equal(15, import.Attempts[0].RoundtripMilliseconds);
        Assert.False(import.Attempts[1].IsSuccess);
        Assert.Equal(TimeSpan.Zero, import.Attempts[2].Timestamp.Offset);
        Assert.Equal(new ImportIssue(6, "unreadable time \"yesterday\""), Assert.Single(import.Issues));
        Assert.Equal(1, import.SkippedRows);
    }

    [Fact]
    public async Task Read_PrefersThePingsSheet()
    {
        using var workbook = new XLWorkbook();
        Row(workbook.AddWorksheet("Other"), 1, "Time", "Latency");
        var pings = workbook.AddWorksheet("Pings");
        Row(pings, 1, "Time", "UTC offset", "Target", "Result", "Latency (ms)");
        Row(pings, 2, new DateTime(2026, 9, 25, 9, 0, 0), "UTC+02:00", "8.8.8.8", "Reply", 20);

        var import = await Read(workbook, "report.xlsx");

        var attempt = Assert.Single(import.Attempts);
        Assert.Equal(new DateTimeOffset(2026, 9, 25, 9, 0, 0, TimeSpan.FromHours(2)), attempt.Timestamp);
        Assert.Equal("8.8.8.8", attempt.TargetHost);
    }

    [Fact]
    public async Task Read_NoPingSheet_IsRefused()
    {
        using var workbook = new XLWorkbook();
        Row(workbook.AddWorksheet("Budget"), 1, "Item", "Price");
        Row(workbook.Worksheet("Budget"), 2, "Router", 89);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => Read(workbook, "budget.xlsx"));

        Assert.StartsWith("budget.xlsx has no sheet with a time column", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Read_NotAWorkbook_IsRefused()
    {
        using var input = new MemoryStream("Timestamp,Latency\n"u8.ToArray());

        var error = await Assert.ThrowsAsync<InvalidDataException>(
            () => new ExcelPingReader().ReadAsync(input, "renamed.xlsx", TestContext.Current.CancellationToken));

        Assert.Equal("renamed.xlsx could not be opened as an Excel workbook.", error.Message);
    }

    [Fact]
    public async Task Importer_PicksTheExcelReaderByExtension()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Sheet1");
        Row(sheet, 1, "Timestamp", "Success");
        Row(sheet, 2, "2026-09-25 09:00:00", true);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        var importer = new PingFileImporter([new PingCsvReader(), new ExcelPingReader()]);

        var import = await importer.ReadAsync(stream, @"C:\Logs\Monday.XLSX", TestContext.Current.CancellationToken);

        Assert.Equal("Monday.XLSX", import.SourceName);
        Assert.Single(import.Attempts);
        Assert.Contains("Excel workbooks (*.xlsx;*.xlsm)|*.xlsx;*.xlsm", importer.FileFilter, StringComparison.Ordinal);
    }

    private static async Task<PingImport> Read(XLWorkbook workbook, string name)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return await new ExcelPingReader().ReadAsync(stream, name, TestContext.Current.CancellationToken);
    }

    private static void Row(IXLWorksheet sheet, int row, params XLCellValue[] values)
    {
        for (var index = 0; index < values.Length; index++)
        {
            sheet.Cell(row, index + 1).Value = values[index];
        }
    }
}
