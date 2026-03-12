namespace PingerTool.Core.Pinging.Models;

public sealed record PingRunSettings(
    string TargetHost,
    int TimeoutMilliseconds,
    int IntervalMilliseconds,
    TimeSpan? RunDuration,
    bool RunForever);
