using System.Text;
using PdfSharp.Pdf.IO;
using PingRunner.Core.Reports;
using PingRunner.Infrastructure.Reports;
using PingRunner.Infrastructure.Tests.Fakes;

namespace PingRunner.Infrastructure.Tests.Reports;

public sealed class PdfReportWriterTests
{
    [Fact]
    public async Task Write_MakesAPdfWithTheTitleAndEveryPage()
    {
        var writer = new PdfReportWriter();
        using var output = new MemoryStream();

        await writer.WriteAsync(SampleReports.Report(), output, TestContext.Current.CancellationToken);

        var bytes = output.ToArray();
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        output.Position = 0;
        using var pdf = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.InRange(pdf.PageCount, 2, 6);
        Assert.Equal("Connection report", pdf.Info.Title);
        Assert.Equal(ReportFormat.Pdf, writer.Format);
        Assert.Equal(".pdf", writer.FileExtension);
    }

    [Fact]
    public async Task Write_ADayOfPings_ListsTheFirstOutagesAndSaysHowManyMore()
    {
        // One outage every 100 seconds: 864 in a day, more than the PDF lists.
        var report = SampleReports.Report(seconds: 86_400);
        using var output = new MemoryStream();

        await new PdfReportWriter().WriteAsync(report, output, TestContext.Current.CancellationToken);

        Assert.True(report.Statistics.Outages.Count > PdfReportWriter.MaximumListedOutages);
        output.Position = 0;
        using var pdf = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.InRange(pdf.PageCount, 5, 20);
    }

    [Fact]
    public async Task Write_NoOptionalSections_StillWrites()
    {
        var options = new ReportOptions { IncludeConnection = false, IncludeSpeedTests = false, Title = "Kitchen Wi-Fi, ěščřž" };
        var report = SampleReports.Report(seconds: 20, options) with { Charts = [] };
        using var output = new MemoryStream();

        await new PdfReportWriter().WriteAsync(report, output, TestContext.Current.CancellationToken);

        output.Position = 0;
        using var pdf = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.Equal("Kitchen Wi-Fi, ěščřž", pdf.Info.Title);
    }
}
