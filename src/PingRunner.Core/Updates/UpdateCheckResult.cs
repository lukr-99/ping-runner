namespace PingRunner.Core.Updates;

public sealed record UpdateCheckResult(UpdateStatus Status, ReleaseInfo? Release, string? Error = null);
