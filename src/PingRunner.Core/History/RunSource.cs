namespace PingRunner.Core.History;

/// <summary>Where a stored run's pings came from.</summary>
public enum RunSource
{
    /// <summary>Pinged by this app.</summary>
    Recorded,

    /// <summary>Read from a file; <see cref="RunRecord.SourceName"/> names it.</summary>
    Imported,
}
