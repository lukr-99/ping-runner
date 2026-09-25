namespace PingRunner.Core.History;

/// <summary>Looking after the history as a whole: its size, a full backup, restoring one, and clearing it.</summary>
public interface IHistoryMaintenance
{
    /// <summary>Where the history lives, for the settings page.</summary>
    string Location { get; }

    /// <summary>Why the history is unavailable or was reset at start; null when all is well.</summary>
    string? Problem { get; }

    Task<HistoryStorageInfo> GetInfoAsync(CancellationToken cancellationToken);

    /// <summary>Writes a complete copy of the history to <paramref name="destinationPath"/>.</summary>
    Task BackupAsync(string destinationPath, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the history with a backup after checking it is a readable Ping Runner history. On any
    /// problem the current history stays as it was.
    /// </summary>
    /// <returns>What the restored history holds.</returns>
    Task<HistoryStorageInfo> RestoreAsync(string sourcePath, CancellationToken cancellationToken);

    /// <summary>Deletes every run and speed test.</summary>
    Task ClearAsync(CancellationToken cancellationToken);
}
