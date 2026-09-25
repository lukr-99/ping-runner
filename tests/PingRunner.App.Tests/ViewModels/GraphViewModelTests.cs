using System.IO;
using ClosedXML.Excel;
using PingRunner.App.Tests.Hosting;
using PingRunner.Core.Graphing;

namespace PingRunner.App.Tests.ViewModels;

[Collection(WpfCollection.Name)]
public sealed class GraphViewModelTests
{
    private const string SampleCsv = """
        Timestamp,TargetHost,IsSuccess,RoundtripTimeMilliseconds,Details
        2026-09-25T10:00:00.0000000+02:00,1.1.1.1,true,20,Reply from 1.1.1.1
        2026-09-25T10:00:01.0000000+02:00,1.1.1.1,false,,Request timed out
        2026-09-25T10:00:02.0000000+02:00,1.1.1.1,true,24,Reply from 1.1.1.1
        """;

    [Fact]
    public Task Refresh_LiveSession_ShowsTheRangeAndItsStatistics() => WpfHost.RunAsync(async () =>
    {
        using var app = TestApp.Create();
        await app.PingAsync(400);

        var graph = app.Graph.Graph;
        graph.Refresh();

        Assert.Equal(300, graph.RangedAttempts.Count);
        Assert.StartsWith($"Showing 300 of {app.Graph.Session.Attempts.Count} pings", graph.ViewText, StringComparison.Ordinal);
        Assert.Equal("Live session · 8.8.8.8", graph.SourceText);
    });

    [Fact]
    public Task Zoom_NarrowsTheStatisticsToWhatIsVisible() => WpfHost.RunAsync(async () =>
    {
        using var app = TestApp.Create();
        await app.PingAsync(400);
        var graph = app.Graph.Graph;
        graph.Refresh();

        graph.Zoom = new GraphZoom(4, 1);

        Assert.StartsWith($"Showing 75 of {app.Graph.Session.Attempts.Count} pings", graph.ViewText, StringComparison.Ordinal);
    });

    [Fact]
    public Task Import_ValidFile_SwitchesToItAndBack() => WpfHost.RunAsync(async () =>
    {
        using var app = TestApp.Create();
        var path = Path.Combine(Path.GetTempPath(), $"pingrunner-{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(path, SampleCsv);
        try
        {
            app.Desktop.FileToOpen = path;
            var graph = app.Graph.Graph;

            await graph.ImportCommand.ExecuteAsync(null);

            Assert.True(graph.IsImported);
            Assert.Equal(3, graph.RangedAttempts.Count);
            Assert.Equal("33.3 %", graph.Stats.Loss.Replace(',', '.'));
            graph.UseLiveDataCommand.Execute(null);
            Assert.False(graph.IsImported);
            Assert.Empty(graph.RangedAttempts);
        }
        finally
        {
            File.Delete(path);
        }
    });

    [Fact]
    public Task Import_ExcelWorkbook_ShowsItAndSaysWhatWasRead() => WpfHost.RunAsync(async () =>
    {
        using var app = TestApp.Create();
        var path = Path.Combine(Path.GetTempPath(), $"pingrunner-{Guid.NewGuid():N}.xlsx");
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("Log");
            sheet.Cell(1, 1).Value = "Time";
            sheet.Cell(1, 2).Value = "Latency";
            sheet.Cell(2, 1).Value = new DateTime(2026, 9, 25, 10, 0, 0);
            sheet.Cell(2, 2).Value = 20;
            sheet.Cell(3, 1).Value = new DateTime(2026, 9, 25, 10, 0, 1);
            sheet.Cell(3, 2).Value = 22;
            workbook.SaveAs(path);
        }

        app.Desktop.FileToOpen = path;
        try
        {
            var graph = app.Graph.Graph;

            await graph.ImportCommand.ExecuteAsync(null);

            Assert.True(graph.IsImported);
            Assert.Equal(2, graph.RangedAttempts.Count);
            Assert.Equal($"Read 2 pings from {Path.GetFileName(path)}.", graph.Notice);
            Assert.Equal($"Imported · {Path.GetFileName(path)}", graph.SourceText);

            graph.UseLiveDataCommand.Execute(null);
            Assert.False(graph.HasNotice);
        }
        finally
        {
            File.Delete(path);
        }
    });

    [Fact]
    public Task Import_BrokenFile_ReportsTheLine() => WpfHost.RunAsync(async () =>
    {
        using var app = TestApp.Create();
        var path = Path.Combine(Path.GetTempPath(), $"pingrunner-{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(path, "Timestamp,TargetHost,IsSuccess,RoundtripTimeMilliseconds,Details\nyesterday,1.1.1.1,true,1,x\n");
        try
        {
            await app.Graph.Graph.ImportFromAsync(path);

            var error = Assert.Single(app.Desktop.Errors);
            Assert.Contains("Line 2", error.Message, StringComparison.Ordinal);
            Assert.False(app.Graph.Graph.IsImported);
        }
        finally
        {
            File.Delete(path);
        }
    });

    [Fact]
    public Task ExportAll_WritesEveryPing() => WpfHost.RunAsync(async () =>
    {
        using var app = TestApp.Create();
        await app.PingAsync(12);
        var path = Path.Combine(Path.GetTempPath(), $"pingrunner-{Guid.NewGuid():N}.csv");
        app.Desktop.FileToSave = path;
        try
        {
            await app.Graph.Graph.ExportAllCommand.ExecuteAsync(null);

            Assert.Equal(app.Graph.Session.Attempts.Count + 1, (await File.ReadAllLinesAsync(path)).Length);
        }
        finally
        {
            File.Delete(path);
        }
    });

    [Fact]
    public Task SuggestedFileName_NamesTheTarget() => WpfHost.RunAsync(async () =>
    {
        using var app = TestApp.Create();
        await app.PingAsync(2);

        Assert.StartsWith("8.8.8.8-view-", app.Graph.Graph.SuggestedFileName("view", "csv"), StringComparison.Ordinal);
    });
}
