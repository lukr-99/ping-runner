using PingRunner.Core.Pinging;

namespace PingRunner.Core.Settings;

/// <summary>
/// Everything Ping Runner remembers between runs: appearance, the ping form's last values, recent
/// targets and speed-test preferences. Stored as JSON with <see cref="Version"/> so a later release
/// can migrate it (ARCHITECTURE.md, Data). Values from the file are untrusted and pass through
/// <see cref="Normalized"/> before use.
/// </summary>
public sealed record AppSettings
{
    public const int CurrentVersion = 1;
    public const int MaximumRecentTargets = 8;
    public const string DefaultTarget = "8.8.8.8";
    public const string DefaultDuration = "00:05:00";

    public int Version { get; init; } = CurrentVersion;

    public ThemeMode Theme { get; init; } = ThemeMode.System;

    public AccentChoice Accent { get; init; } = AccentChoice.Teal;

    public string Target { get; init; } = DefaultTarget;

    public IReadOnlyList<string> RecentTargets { get; init; } = [DefaultTarget, "1.1.1.1"];

    public int IntervalMilliseconds { get; init; } = 1000;

    public int TimeoutMilliseconds { get; init; } = 1000;

    /// <summary>"hh:mm:ss" when the run has a fixed length; ignored while <see cref="RunUntilStopped"/>.</summary>
    public string Duration { get; init; } = DefaultDuration;

    public bool RunUntilStopped { get; init; } = true;

    /// <summary>The live session keeps this many attempts; older ones drop off (export first to keep them).</summary>
    public int MaximumStoredAttempts { get; init; } = 50_000;

    public int SpeedTestSeconds { get; init; } = 10;

    public int SpeedTestStreams { get; init; } = 4;

    public static AppSettings Default { get; } = new();

    /// <summary>Puts <paramref name="host"/> first in the recent targets, without duplicates, keeping at most eight.</summary>
    public AppSettings WithRecentTarget(string host)
    {
        ArgumentNullException.ThrowIfNull(host);
        var trimmed = host.Trim();
        if (!IsUsableHost(trimmed))
        {
            return this;
        }

        return this with
        {
            Target = trimmed,
            RecentTargets =
            [
                trimmed,
                .. RecentTargets
                    .Where(target => !string.Equals(target, trimmed, StringComparison.OrdinalIgnoreCase))
                    .Take(MaximumRecentTargets - 1),
            ],
        };
    }

    /// <summary>Clamps every value into its allowed range and drops anything unusable.</summary>
    public AppSettings Normalized() => this with
    {
        Version = CurrentVersion,
        Theme = Enum.IsDefined(Theme) ? Theme : ThemeMode.System,
        Accent = Enum.IsDefined(Accent) ? Accent : AccentChoice.Teal,
        Target = IsUsableHost(Target) ? Target.Trim() : DefaultTarget,
        RecentTargets =
        [
            .. (RecentTargets ?? [])
                .Where(IsUsableHost)
                .Select(target => target.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaximumRecentTargets),
        ],
        IntervalMilliseconds = Math.Clamp(
            IntervalMilliseconds, PingRunSettings.MinimumIntervalMilliseconds, PingRunSettings.MaximumIntervalMilliseconds),
        TimeoutMilliseconds = Math.Clamp(
            TimeoutMilliseconds, PingRunSettings.MinimumTimeoutMilliseconds, PingRunSettings.MaximumTimeoutMilliseconds),
        Duration = TimeSpan.TryParse(Duration, out var duration) && duration > TimeSpan.Zero ? Duration : DefaultDuration,
        MaximumStoredAttempts = Math.Clamp(MaximumStoredAttempts, 1_000, 500_000),
        SpeedTestSeconds = Math.Clamp(SpeedTestSeconds, 3, 60),
        SpeedTestStreams = Math.Clamp(SpeedTestStreams, 1, 16),
    };

    private static bool IsUsableHost(string? host) =>
        !string.IsNullOrWhiteSpace(host)
        && host.Trim().Length <= PingRunSettings.MaximumHostLength
        && !host.Trim().Any(char.IsWhiteSpace);
}
