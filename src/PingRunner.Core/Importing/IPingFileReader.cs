namespace PingRunner.Core.Importing;

/// <summary>Reads pings from one kind of file. Files are untrusted: every row is checked.</summary>
public interface IPingFileReader
{
    /// <summary>Extensions this reader handles, lower case with the dot, for example ".csv".</summary>
    IReadOnlyList<string> Extensions { get; }

    /// <summary>For the file dialog, for example "CSV files".</summary>
    string Description { get; }

    /// <exception cref="InvalidDataException">The file holds no readable pings; the message says why.</exception>
    Task<PingImport> ReadAsync(Stream input, string sourceName, CancellationToken cancellationToken);
}
