using System.Globalization;
using System.Text;
using PingRunner.Core.Pinging;

namespace PingRunner.Core.Sessions;

/// <summary>
/// Writes the session file format: CSV with the header
/// <c>Timestamp,TargetHost,IsSuccess,RoundtripTimeMilliseconds,Details</c>, one attempt per row,
/// timestamps in ISO 8601 with their offset, the same format Ping Runner 1.x wrote. Line breaks in
/// details become spaces, so a row is always one line. <see cref="Importing.PingCsvReader"/> reads it
/// back, along with versions a spreadsheet has saved again.
/// </summary>
public static class PingAttemptCsv
{
    public const string Header = "Timestamp,TargetHost,IsSuccess,RoundtripTimeMilliseconds,Details";

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

    private static string Escape(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) < 0
            ? value
            : $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
