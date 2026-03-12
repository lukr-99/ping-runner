using System.Globalization;
using System.IO;
using System.Text;
using PingerTool.Core.Pinging.Models;

namespace PingerTool.Features.Data.Services;

public sealed class PingAttemptCsvSerializer
{
    public async Task ExportAsync(IEnumerable<PingAttemptResult> attempts, Stream outputStream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attempts);
        ArgumentNullException.ThrowIfNull(outputStream);

        await using var writer = new StreamWriter(outputStream, new UTF8Encoding(false), leaveOpen: true);
        await writer.WriteLineAsync("Timestamp,TargetHost,IsSuccess,RoundtripTimeMilliseconds,Details");

        foreach (var attempt in attempts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = string.Join(
                ",",
                Escape(attempt.Timestamp.ToString("O", CultureInfo.InvariantCulture)),
                Escape(attempt.TargetHost),
                Escape(attempt.IsSuccess ? "true" : "false"),
                Escape(attempt.RoundtripTimeMilliseconds?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
                Escape(attempt.Details));

            await writer.WriteLineAsync(line);
        }

        await writer.FlushAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PingAttemptResult>> ImportAsync(Stream inputStream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputStream);

        using var reader = new StreamReader(inputStream, Encoding.UTF8, leaveOpen: true);
        var importedAttempts = new List<PingAttemptResult>();
        var lineIndex = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(cancellationToken);

            if (line is null)
            {
                break;
            }

            lineIndex++;

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (lineIndex == 1 && line.StartsWith("Timestamp,", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fields = ParseCsvLine(line);

            if (fields.Count < 5)
            {
                throw new InvalidOperationException($"Line {lineIndex} is not a valid ping export row.");
            }

            importedAttempts.Add(
                new PingAttemptResult(
                    fields[1],
                    DateTimeOffset.Parse(fields[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                    bool.Parse(fields[2]),
                    string.IsNullOrWhiteSpace(fields[3]) ? null : long.Parse(fields[3], CultureInfo.InvariantCulture),
                    fields[4]));
        }

        return importedAttempts
            .OrderBy(attempt => attempt.Timestamp)
            .ToList();
    }

    private static IReadOnlyList<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var insideQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var currentCharacter = line[index];

            if (currentCharacter == '"')
            {
                if (insideQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                    continue;
                }

                insideQuotes = !insideQuotes;
                continue;
            }

            if (currentCharacter == ',' && !insideQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(currentCharacter);
        }

        fields.Add(current.ToString());
        return fields;
    }

    private static string Escape(string value)
    {
        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
