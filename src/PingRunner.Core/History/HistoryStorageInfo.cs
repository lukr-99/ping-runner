namespace PingRunner.Core.History;

/// <summary>What the history holds and how much disk it takes.</summary>
public sealed record HistoryStorageInfo(int Runs, long Attempts, int SpeedTests, long SizeBytes)
{
    public static HistoryStorageInfo Empty { get; } = new(0, 0, 0, 0);
}
