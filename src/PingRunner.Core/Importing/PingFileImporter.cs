namespace PingRunner.Core.Importing;

/// <summary>
/// Picks the reader for a file by its extension (CSV when there is none that fits) and builds the file
/// dialog filter from the readers it has.
/// </summary>
public sealed class PingFileImporter(IReadOnlyList<IPingFileReader> readers)
{
    /// <summary>For an open-file dialog: all readable files first, then each kind, then everything.</summary>
    public string FileFilter
    {
        get
        {
            var all = string.Join(";", readers.SelectMany(reader => reader.Extensions).Select(extension => "*" + extension));
            var each = readers.Select(reader =>
            {
                var patterns = string.Join(";", reader.Extensions.Select(extension => "*" + extension));
                return $"{reader.Description} ({patterns})|{patterns}";
            });
            return string.Join("|", [$"Ping data ({all})|{all}", .. each, "All files (*.*)|*.*"]);
        }
    }

    /// <exception cref="InvalidDataException">The file holds no readable pings.</exception>
    public Task<PingImport> ReadAsync(Stream input, string fileName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var reader = readers.FirstOrDefault(candidate => candidate.Extensions.Contains(extension))
            ?? readers.FirstOrDefault(candidate => candidate is PingCsvReader)
            ?? throw new InvalidDataException($"Ping Runner cannot read {extension} files.");
        return reader.ReadAsync(input, Path.GetFileName(fileName), cancellationToken);
    }
}
