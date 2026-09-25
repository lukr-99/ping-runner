using System.Globalization;
using PingRunner.Core.Pinging;

namespace PingRunner.App.ViewModels;

/// <summary>One line of the recent-pings list.</summary>
public sealed class AttemptRowViewModel(PingAttempt attempt)
{
    public string Time { get; } = attempt.Timestamp.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture);

    public string Latency { get; } = attempt.RoundtripMilliseconds is { } roundtrip ? $"{roundtrip} ms" : "Lost";

    public string Details { get; } = attempt.Details;

    public bool IsLost { get; } = !attempt.IsSuccess;
}
