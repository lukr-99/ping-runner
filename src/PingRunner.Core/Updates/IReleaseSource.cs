namespace PingRunner.Core.Updates;

/// <summary>Where releases are published.</summary>
public interface IReleaseSource
{
    /// <summary>The newest published, non-draft release; null when none is published yet.</summary>
    Task<ReleaseInfo?> GetLatestAsync(CancellationToken cancellationToken);
}
