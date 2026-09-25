using System.Text;
using PingRunner.Core.Pinging;

namespace PingRunner.Core.Importing;

/// <summary>
/// Reads pings from CSV: Ping Runner's own export, and the same file after a spreadsheet saved it
/// again (semicolons or tabs instead of commas, local date formats, columns moved or renamed). The
/// delimiter is taken from the header line. Rows that cannot be read are left out and listed; a file
/// with no readable rows is refused.
/// </summary>
public sealed class PingCsvReader : IPingFileReader
{
    public const int MaximumRows = 2_000_000;
    public const int MaximumLineLength = 4_096;

    public IReadOnlyList<string> Extensions { get; } = [".csv", ".txt", ".tsv"];

    public string Description => "CSV files";

    public async Task<PingImport> ReadAsync(Stream input, string sourceName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        using var reader = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);

        PingRowReader? rows = null;
        var delimiter = ',';
        var attempts = new List<PingAttempt>();
        var issues = new List<ImportIssue>();
        var skipped = 0;
        var lineNumber = 0;

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (rows is null)
            {
                delimiter = DelimiterOf(line);
                rows = PingRowReader.FromHeader(Split(line, delimiter))
                    ?? throw new InvalidDataException($"{sourceName} has no header with a time column and a result or latency column, so it is not a ping file.");
                continue;
            }

            string? problem;
            if (line.Length > MaximumLineLength)
            {
                problem = $"longer than {MaximumLineLength:N0} characters";
            }
            else if (attempts.Count == MaximumRows)
            {
                throw new InvalidDataException($"{sourceName} has more than {MaximumRows:N0} pings.");
            }
            else if (rows.Read(Split(line, delimiter), out problem) is { } attempt)
            {
                attempts.Add(attempt);
                continue;
            }

            skipped++;
            if (issues.Count < PingImport.MaximumListedIssues)
            {
                issues.Add(new ImportIssue(lineNumber, problem ?? "unreadable"));
            }
        }

        if (rows is null)
        {
            throw new InvalidDataException($"{sourceName} is empty.");
        }

        if (attempts.Count == 0)
        {
            throw new InvalidDataException(issues.Count > 0
                ? $"{sourceName} has no readable pings ({issues[0]})."
                : $"{sourceName} has a header but no pings.");
        }

        return new PingImport(sourceName, [.. attempts.OrderBy(attempt => attempt.Timestamp)], skipped, issues);
    }

    // The separator the header uses most: a comma, a semicolon (spreadsheets in many locales) or a tab.
    private static char DelimiterOf(string header)
    {
        var best = new[] { ',', ';', '\t' }
            .Select(candidate => (Candidate: candidate, Count: header.Count(character => character == candidate)))
            .MaxBy(entry => entry.Count);
        return best.Count > 0 ? best.Candidate : ',';
    }

    private static List<string?> Split(string line, char delimiter)
    {
        var fields = new List<string?>();
        var current = new StringBuilder();
        var quoted = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                    continue;
                }

                quoted = !quoted;
                continue;
            }

            if (character == delimiter && !quoted)
            {
                fields.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        fields.Add(current.ToString());
        return fields;
    }
}
