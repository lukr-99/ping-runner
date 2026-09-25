using PingRunner.Core.Pinging;
using PingRunner.Infrastructure.History;
using PingRunner.Infrastructure.Tests.Fakes;

namespace PingRunner.Infrastructure.Tests.History;

/// <summary>A history database in a temporary folder, with the stores built on it.</summary>
public sealed class HistoryFixture : IDisposable
{
    public static readonly DateTimeOffset Start = new(2026, 9, 25, 14, 0, 0, TimeSpan.FromHours(2));

    private readonly TemporaryDirectory folder = new();

    public HistoryFixture()
    {
        Database = SqliteHistoryDatabase.Open(Path);
        Runs = new SqlitePingRunHistory(Database);
        SpeedTests = new SqliteSpeedTestHistory(Database);
    }

    public string Path => folder.File("history.db");

    public SqliteHistoryDatabase Database { get; }

    public SqlitePingRunHistory Runs { get; }

    public SqliteSpeedTestHistory SpeedTests { get; }

    public string File(string name) => folder.File(name);

    public static PingRunSettings Settings(string host = "8.8.8.8", TimeSpan? duration = null) =>
        PingRunSettings.TryCreate(host, 1000, 1000, duration, out _)!;

    public static List<PingAttempt> Attempts(int count, string host = "8.8.8.8") =>
    [
        .. Enumerable.Range(0, count).Select(index => index % 7 == 3
            ? new PingAttempt(host, Start.AddSeconds(index), false, null, "Request timed out")
            : new PingAttempt(host, Start.AddSeconds(index), true, 15 + (index % 5), $"Reply from {host}")),
    ];

    public void Dispose()
    {
        Database.Dispose();
        folder.Dispose();
    }
}
