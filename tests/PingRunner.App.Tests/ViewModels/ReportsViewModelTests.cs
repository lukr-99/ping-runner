using System.IO;
using ClosedXML.Excel;
using PdfSharp.Pdf.IO;
using PingRunner.App.Reports;
using PingRunner.App.Shell;
using PingRunner.App.Tests.Hosting;

namespace PingRunner.App.Tests.ViewModels;

[Collection(WpfCollection.Name)]
public sealed class ReportsViewModelTests
{
    [Fact]
    public Task Open_WithAHistoryButNoSession_StartsOnTheNewestRun() => WpfHost.RunAsync(async () =>
    {
        using var app = TestApp.Create();
        await app.SeedHistoryAsync();
        var reports = app.Graph.Reports;

        await reports.EnsureLoadedAsync();

        Assert.Equal(ReportSourceKind.StoredRun, reports.SourceKind);
        Assert.True(reports.IsStoredRun);
        Assert.Equal(3, reports.Runs.Count);
        Assert.Equal("192.168.1.1", reports.SelectedRun!.Run.TargetHost);
        Assert.Equal(300, reports.PingCount);
        Assert.StartsWith("192.168.1.1  ·  ", reports.PreviewHeading, StringComparison.Ordinal);
        Assert.StartsWith("300 pings to 192.168.1.1", reports.Findings[0], StringComparison.Ordinal);
        Assert.False(reports.GraphShowsOtherData);
    });

    [Fact]
    public Task Open_WithNothingToReport_SaysWhatToDo() => WpfHost.RunAsync(async () =>
    {
        using var app = TestApp.Create();
        var reports = app.Graph.Reports;

        await reports.EnsureLoadedAsync();

        Assert.Equal(ReportSourceKind.LiveSession, reports.SourceKind);
        Assert.Equal(0, reports.PingCount);
        Assert.StartsWith("The live session has no pings yet.", reports.PreviewMessage, StringComparison.Ordinal);
        Assert.False(reports.SavePdfCommand.CanExecute(null));
    });

    [Fact]
    public Task HistoryPeriod_JoinsTheTargetsRunsInsideTheDates() => WpfHost.RunAsync(async () =>
    {
        using var app = TestApp.Create();
        await app.SeedHistoryAsync();
        var reports = app.Graph.Reports;
        await reports.EnsureLoadedAsync();

        reports.SelectedTarget = "8.8.8.8";
        reports.IsHistoryPeriod = true;
        await reports.RefreshPreviewCommand.ExecuteAsync(null);

        Assert.Equal(3_600, reports.PingCount);

        // The 8.8.8.8 run is 50 hours old: a period of only today leaves it out.
        reports.PeriodFrom = app.Time.GetLocalNow().Date;
        await reports.RefreshPreviewCommand.ExecuteAsync(null);

        Assert.Equal(0, reports.PingCount);
        Assert.StartsWith("No stored pings to 8.8.8.8", reports.PreviewMessage, StringComparison.Ordinal);
    });

    [Fact]
    public Task SavePdf_WritesTheReportAndOffersToOpenIt() => WpfHost.RunAsync(async () =>
    {
        using var app = TestApp.Create();
        await app.SeedHistoryAsync();
        var reports = app.Graph.Reports;
        await reports.EnsureLoadedAsync();
        var path = Path.Combine(Path.GetTempPath(), $"pingrunner-{Guid.NewGuid():N}.pdf");
        app.Desktop.FileToSave = path;
        reports.Title = "Router check";
        try
        {
            await reports.SavePdfCommand.ExecuteAsync(null);

            Assert.Empty(app.Desktop.Errors);
            using (var pdf = PdfReader.Open(path, PdfDocumentOpenMode.Import))
            {
                Assert.Equal("Router check", pdf.Info.Title);
                Assert.True(pdf.PageCount >= 2);
            }

            Assert.Equal(path, reports.SavedPath);
            Assert.StartsWith($"Saved {Path.GetFileName(path)}", reports.SavedText, StringComparison.Ordinal);
            reports.OpenSavedCommand.Execute(null);
            reports.ShowSavedInFolderCommand.Execute(null);
            Assert.Equal([path, Path.GetDirectoryName(path)!], app.Desktop.Opened);
        }
        finally
        {
            File.Delete(path);
        }
    });

    [Fact]
    public Task SaveExcel_LeavesThePublicAddressOutUnlessAskedFor() => WpfHost.RunAsync(async () =>
    {
        using var app = TestApp.Create();
        await app.PingAsync(120);
        var reports = app.Graph.Reports;
        await reports.EnsureLoadedAsync();
        var path = Path.Combine(Path.GetTempPath(), $"pingrunner-{Guid.NewGuid():N}.xlsx");
        app.Desktop.FileToSave = path;
        try
        {
            await reports.SaveExcelCommand.ExecuteAsync(null);
            using (var workbook = new XLWorkbook(path))
            {
                Assert.Equal(ReportSourceKind.LiveSession, reports.SourceKind);
                Assert.Equal(reports.PingCount + 1, workbook.Worksheet("Pings").LastRowUsed()!.RowNumber());
                Assert.DoesNotContain(workbook.Worksheet("Connection").CellsUsed(), cell => cell.GetText() == "203.0.113.42");
            }

            reports.IncludePublicIp = true;
            await reports.SaveExcelCommand.ExecuteAsync(null);
            using (var workbook = new XLWorkbook(path))
            {
                Assert.Contains(workbook.Worksheet("Connection").CellsUsed(), cell => cell.GetText() == "203.0.113.42");
            }
        }
        finally
        {
            File.Delete(path);
        }
    });

    [Fact]
    public Task Save_Cancelled_WritesNothing() => WpfHost.RunAsync(async () =>
    {
        using var app = TestApp.Create();
        await app.SeedHistoryAsync();
        await app.Graph.Reports.EnsureLoadedAsync();
        app.Desktop.FileToSave = null;

        await app.Graph.Reports.SavePdfCommand.ExecuteAsync(null);

        Assert.Null(app.Graph.Reports.SavedPath);
        Assert.Empty(app.Desktop.Errors);
    });

    [Fact]
    public Task GraphReport_TakesWhatTheGraphShows() => WpfHost.RunAsync(async () =>
    {
        using var app = TestApp.Create();
        await app.SeedHistoryAsync();
        var opened = new List<AppPage>();
        app.Graph.Navigation.Requested += (_, page) => opened.Add(page);
        await app.Graph.History.EnsureLoadedAsync();
        await app.Graph.History.OpenRunCommand.ExecuteAsync(app.Graph.History.Runs.First(row => row.Target == "1.1.1.1"));

        app.Graph.Graph.RequestReportCommand.Execute(null);
        await app.AdvanceUntilAsync(() => opened.Contains(AppPage.Reports), TimeSpan.FromMilliseconds(10));

        var reports = app.Graph.Reports;
        Assert.Equal(ReportSourceKind.GraphView, reports.SourceKind);
        Assert.Equal(900, reports.PingCount);
        Assert.StartsWith("History · 1.1.1.1", reports.GraphLabel, StringComparison.Ordinal);
    });
}
