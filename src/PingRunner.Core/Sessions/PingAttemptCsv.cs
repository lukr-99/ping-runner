using System.Globalization;
using System.Text;
using PingRunner.Core.Pinging;

namespace PingRunner.Core.Sessions;

/// <summary>
/// The session file format: CSV with the header
/// <c>Timestamp,TargetHost,IsSuccess,RoundtripTimeMilliseconds,Details</c>, one attempt per row,
/// timestamps in ISO 8601 with their offset. It is the same format Ping Runner 1.x exported, so old
/// files still import. Line breaks in details become spaces, so a row is always one line. Imported
/// files are untrusted: every row is checked and a bad one is reported
/// with its line number instead of being half-loaded.
/// </summary>
public static class PingAttemptCsv
{
    public const string Header = "Timestamp,TargetHost,IsSuccess,RoundtripTimeMilliseconds,Details";
    public const int MaximumRows = 2_000_000;
    public const int MaximumLineLength = 4_096;

    public static async Task ExportAsync(
        IEnumerable<PingAttempt> attempts,
        Stream output,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attempts);
        ArgumentNullException.ThrowIfNull(output);

        var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true) { NewLine = "\n" };
        await using (writer.ConfigureAwait(false))
        {
            await writer.WriteLineAsync(Header).ConfigureAwait(false);
            foreach (var attempt in attempts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await writer.WriteLineAsync(string.Join(
                    ',',
                    Escape(attempt.Timestamp.ToString("O", CultureInfo.InvariantCulture)),
                    Escape(attempt.TargetHost),
                    attempt.IsSuccess ? "true" : "false",
                    attempt.RoundtripMilliseconds?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    Escape(attempt.Details.ReplaceLineEndings(" ")))).ConfigureAwait(false);
            }

            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <returns>The attempts ordered by time.</returns>
    /// <exception cref="InvalidDataException">A row is malformed; the message names the line.</exception>
    public static async Task<IReadOnlyList<PingAttempt>> ImportAsync(Stream input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        using var reader = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var attempts = new List<PingAttempt>();
        var lineNumber = 0;

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (lineNumber == 1 && line.StartsWith("Timestamp,", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (line.Length > MaximumLineLength)
            {
                throw new InvalidDataException($"Line {lineNumber} is longer than {MaximumLineLength} characters.");
            }

            if (attempts.Count == MaximumRows)
            {
                throw new InvalidDataException($"The file has more than {MaximumRows:N0} attempts.");
            }

            attempts.Add(ParseRow(line, lineNumber));
        }

        return [.. attempts.OrderBy(attempt => attempt.Timestamp)];
    }

    private static PingAttempt ParseRow(string line, int lineNumber)
    {
        var fields = Split(line);
        if (fields.Count < 5)
        {
            throw new InvalidDataException($"Line {lineNumber} has {fields.Count} fields; a ping row has 5.");
        }

        if (!DateTimeOffset.TryParse(fields[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var timestamp))
        {
            throw new InvalidDataException($"Line {lineNumber} has an unreadable timestamp: {fields[0]}");
        }

        var host = fields[1].Trim();
        if (host.Length is 0 or > PingRunSettings.MaximumHostLength)
        {
            throw new InvalidDataException($"Line {lineNumber} has no valid target host.");
        }

        if (!bool.TryParse(fields[2], out var isSuccess))
        {
            throw new InvalidDataException($"Line {lineNumber} has IsSuccess '{fields[2]}'; expected true or false.");
        }

        long? roundtrip = null;
        if (!string.IsNullOrWhiteSpace(fields[3]))
        {
            if (!long.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            {
                throw new InvalidDataException($"Line {lineNumber} has an unreadable round-trip time: {fields[3]}");
            }

            roundtrip = value;
        }

        return new PingAttempt(host, timestamp, isSuccess, isSuccess ? roundtrip : null, fields[4]);
    }

    private static List<string> Split(string line)
    {
        var fields = new List<string>();
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

            if (character == ',' && !quoted)
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

    private static string Escape(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) < 0
            ? value
            : $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
