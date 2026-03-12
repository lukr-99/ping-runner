namespace PingerTool.Core.Pinging.Models;

public sealed record PingAttemptResult(
    string TargetHost,
    DateTimeOffset Timestamp,
    bool IsSuccess,
    long? RoundtripTimeMilliseconds,
    string Details)
{
    public string TimestampDisplay => Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff");

    public string OutcomeDisplay => IsSuccess ? "Success" : "Failed";

    public string RoundtripDisplay => RoundtripTimeMilliseconds?.ToString() ?? "-";
}
