using System.Globalization;
using ClosedXML.Excel;
using PingRunner.Core.Importing;
using PingRunner.Core.Pinging;

namespace PingRunner.Infrastructure.Importing;

/// <summary>
/// Reads pings from an Excel workbook: a sheet named "Pings" first (Ping Runner's own report), then
/// any sheet with a header row, within its first rows, that has a time column and a result or latency
/// column. Typed cells (dates, numbers) are read as they are; formulas are read by their saved value,
/// never recalculated. Rows that cannot be read are left out and listed by row number.
/// </summary>
public sealed class ExcelPingReader : IPingFileReader
{
    public const long MaximumFileBytes = 200L * 1024 * 1024;
    public const int HeaderSearchRows = 20;

    public IReadOnlyList<string> Extensions { get; } = [".xlsx", ".xlsm"];

    public string Description => "Excel workbooks";

    public async Task<PingImport> ReadAsync(Stream input, string sourceName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(sourceName);

        var buffer = new MemoryStream();
        await using (buffer.ConfigureAwait(false))
        {
            var chunk = new byte[81_920];
            int read;
            while ((read = await input.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + read > MaximumFileBytes)
                {
                    throw new InvalidDataException($"{sourceName} is larger than {MaximumFileBytes / (1024 * 1024)} MB.");
                }

                buffer.Write(chunk, 0, read);
            }

            buffer.Position = 0;
            return await Task.Run(() => Read(buffer, sourceName, cancellationToken), cancellationToken).ConfigureAwait(false);
        }
    }

    private static PingImport Read(Stream stream, string sourceName, CancellationToken cancellationToken)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvalidDataException($"{sourceName} could not be opened as an Excel workbook.", exception);
        }

        using (workbook)
        {
            var sheets = workbook.Worksheets
                .OrderBy(sheet => string.Equals(sheet.Name, "Pings", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ToList();
            foreach (var sheet in sheets)
            {
                if (TryRead(sheet, sourceName, cancellationToken) is { } import)
                {
                    return import;
                }
            }
        }

        throw new InvalidDataException(
            $"{sourceName} has no sheet with a time column and a result or latency column, so it holds no pings Ping Runner can read.");
    }

    private static PingImport? TryRead(IXLWorksheet sheet, string sourceName, CancellationToken cancellationToken)
    {
        if (sheet.RangeUsed() is not { } used)
        {
            return null;
        }

        var first = used.FirstColumn().ColumnNumber();
        var last = used.LastColumn().ColumnNumber();
        PingRowReader? rows = null;
        var headerRow = 0;
        foreach (var row in sheet.RowsUsed().Take(HeaderSearchRows))
        {
            rows = PingRowReader.FromHeader(Cells(row, first, last));
            if (rows is not null)
            {
                headerRow = row.RowNumber();
                break;
            }
        }

        if (rows is null)
        {
            return null;
        }

        var attempts = new List<PingAttempt>();
        var issues = new List<ImportIssue>();
        var skipped = 0;
        foreach (var row in sheet.RowsUsed(row => row.RowNumber() > headerRow))
        {
            if (attempts.Count % 10_000 == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var cells = Cells(row, first, last);
            if (cells.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            if (attempts.Count == PingCsvReader.MaximumRows)
            {
                throw new InvalidDataException($"{sourceName} has more than {PingCsvReader.MaximumRows:N0} pings.");
            }

            if (rows.Read(cells, out var problem) is { } attempt)
            {
                attempts.Add(attempt);
                continue;
            }

            skipped++;
            if (issues.Count < PingImport.MaximumListedIssues)
            {
                issues.Add(new ImportIssue(row.RowNumber(), problem ?? "unreadable"));
            }
        }

        if (attempts.Count == 0)
        {
            throw new InvalidDataException(issues.Count > 0
                ? $"{sourceName} has no readable pings on sheet {sheet.Name} ({issues[0]})."
                : $"{sourceName} has a header on sheet {sheet.Name} but no pings.");
        }

        return new PingImport(sourceName, [.. attempts.OrderBy(attempt => attempt.Timestamp)], skipped, issues);
    }

    private static List<string?> Cells(IXLRow row, int first, int last)
    {
        var cells = new List<string?>(last - first + 1);
        for (var column = first; column <= last; column++)
        {
            cells.Add(Text(row.Cell(column).CachedValue));
        }

        return cells;
    }

    private static string? Text(XLCellValue value) => value.Type switch
    {
        XLDataType.Text => value.GetText(),
        XLDataType.Number => value.GetNumber().ToString("R", CultureInfo.InvariantCulture),
        XLDataType.DateTime => value.GetDateTime().ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture),
        XLDataType.TimeSpan => value.GetTimeSpan().ToString("c", CultureInfo.InvariantCulture),
        XLDataType.Boolean => value.GetBoolean() ? "true" : "false",
        _ => null,
    };
}
