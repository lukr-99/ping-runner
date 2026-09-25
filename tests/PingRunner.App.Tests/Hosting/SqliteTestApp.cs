using System.IO;
using System.Windows;
using Microsoft.Extensions.Time.Testing;
using PingRunner.App.Composition;
using PingRunner.App.Tests.Fakes;
using PingRunner.Core.Settings;
using PingRunner.Core.Updates;
using PingRunner.Infrastructure.History;

namespace PingRunner.App.Tests.Hosting;

/// <summary>
/// The app graph on a real SQLite history file (and fakes for the network), so a test can close the
/// app and open it again on the same file, like a restart.
/// </summary>
public sealed class SqliteTestApp : IDisposable
{
    private SqliteTestApp(AppGraph graph, FakeTimeProvider time)
    {
        Graph = graph;
        Time = time;
    }

    public AppGraph Graph { get; }

    public FakeTimeProvider Time { get; }

    /// <summary>Must run on the WPF host thread.</summary>
    public static SqliteTestApp Open(string historyPath, DateTimeOffset now)
    {
        var time = new FakeTimeProvider(now);
        var database = SqliteHistoryDatabase.Open(historyPath);
        var adapters = new AppAdapters(
            new RealisticPingSender(),
            time,
            new ClockedSpeedEndpoint(time),
            new FixedConnectionInfoSource(FixedConnectionInfoSource.HomeWiFi),
            new FixedPublicIpSource(null),
            new FixedReleaseSource(null),
            new InMemorySettingsStore(AppSettings.Default with { SpeedTestSeconds = 3 }),
            new SqlitePingRunHistory(database),
            new SqliteSpeedTestHistory(database),
            database,
            Path.GetDirectoryName(historyPath)!,
            database);
        var graph = new AppGraph(new BuildInfo(ReleaseVersion.TryParse("2.1.0")!, true), adapters, Application.Current.Resources, new RecordingDesktopServices());
        return new SqliteTestApp(graph, time);
    }

    public async Task AdvanceUntilAsync(Func<bool> done, TimeSpan step)
    {
        for (var guard = 0; guard < 200_000 && !done(); guard++)
        {
            Time.Advance(step);
            await Task.Yield();
        }
    }

    public void Dispose() => Graph.Dispose();
}
