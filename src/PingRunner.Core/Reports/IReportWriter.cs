namespace PingRunner.Core.Reports;

/// <summary>Writes a report in one file format.</summary>
public interface IReportWriter
{
    ReportFormat Format { get; }

    /// <summary>With the dot, for example ".pdf".</summary>
    string FileExtension { get; }

    /// <summary>For the save dialog, for example "PDF document (*.pdf)|*.pdf".</summary>
    string FileFilter { get; }

    Task WriteAsync(ConnectionReport report, Stream output, CancellationToken cancellationToken);
}
