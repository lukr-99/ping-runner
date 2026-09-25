namespace PingRunner.Core.Pinging;

/// <summary>
/// A validated ping run: where to ping, how long to wait for each reply, how often to send, and for
/// how long (null runs until stopped). Build it with <see cref="TryCreate"/>, which explains what is
/// wrong in words the settings form can show.
/// </summary>
public sealed record PingRunSettings
{
    public const int MinimumIntervalMilliseconds = 100;
    public const int MaximumIntervalMilliseconds = 3_600_000;
    public const int MinimumTimeoutMilliseconds = 10;
    public const int MaximumTimeoutMilliseconds = 60_000;
    public const int MaximumHostLength = 253;

    private PingRunSettings(string targetHost, TimeSpan timeout, TimeSpan interval, TimeSpan? duration)
    {
        TargetHost = targetHost;
        Timeout = timeout;
        Interval = interval;
        Duration = duration;
    }

    public string TargetHost { get; }

    public TimeSpan Timeout { get; }

    public TimeSpan Interval { get; }

    /// <summary>How long the run lasts; null runs until stopped.</summary>
    public TimeSpan? Duration { get; }

    public bool RunsUntilStopped => Duration is null;

    public static PingRunSettings? TryCreate(
        string? targetHost,
        int timeoutMilliseconds,
        int intervalMilliseconds,
        TimeSpan? duration,
        out string? error)
    {
        var host = targetHost?.Trim() ?? string.Empty;
        error = host switch
        {
            "" => "Enter a host name or IP address.",
            { Length: > MaximumHostLength } => $"A host name has at most {MaximumHostLength} characters.",
            _ when host.Any(char.IsWhiteSpace) => "A host name cannot contain spaces.",
            _ => null,
        };

        error ??= timeoutMilliseconds is < MinimumTimeoutMilliseconds or > MaximumTimeoutMilliseconds
            ? $"Timeout must be between {MinimumTimeoutMilliseconds} and {MaximumTimeoutMilliseconds:N0} ms."
            : null;

        error ??= intervalMilliseconds is < MinimumIntervalMilliseconds or > MaximumIntervalMilliseconds
            ? $"Interval must be between {MinimumIntervalMilliseconds} and {MaximumIntervalMilliseconds:N0} ms."
            : null;

        error ??= duration is { } span && span <= TimeSpan.Zero
            ? "Duration must be longer than zero, for example 00:30:00."
            : null;

        return error is null
            ? new PingRunSettings(
                host,
                TimeSpan.FromMilliseconds(timeoutMilliseconds),
                TimeSpan.FromMilliseconds(intervalMilliseconds),
                duration)
            : null;
    }
}
