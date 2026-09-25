namespace PingRunner.Core.Updates;

/// <summary>A published release: its version, title and the page to download it from.</summary>
public sealed record ReleaseInfo(ReleaseVersion Version, string Title, Uri PageUrl, DateTimeOffset? PublishedAt);
