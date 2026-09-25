using System.Net;
using System.Net.Http;
using PingRunner.Core.History;
using PingRunner.Core.Network;
using PingRunner.Core.Pinging;
using PingRunner.Core.Settings;
using PingRunner.Core.Throughput;
using PingRunner.Core.Updates;
using PingRunner.Infrastructure.History;
using PingRunner.Infrastructure.Network;
using PingRunner.Infrastructure.Pinging;
using PingRunner.Infrastructure.Settings;
using PingRunner.Infrastructure.Storage;
using PingRunner.Infrastructure.Throughput;
using PingRunner.Infrastructure.Updates;

namespace PingRunner.App.Composition;

/// <summary>
/// Everything that touches the network, the clock or the disk, in one place, so the composition root
/// can run on the real ones (<see cref="ForUser"/>) or on fakes in tests and the screenshot tool.
/// Owns and disposes the HTTP clients it creates.
/// </summary>
public sealed class AppAdapters : IDisposable
{
    private readonly IDisposable[] owned;

    public AppAdapters(
        IPingSender pinger,
        TimeProvider time,
        IThroughputEndpoint speedEndpoint,
        IConnectionInfoSource connections,
        IPublicIpSource publicIp,
        IReleaseSource releases,
        ISettingsStore settingsStore,
        IPingRunHistory pingRuns,
        ISpeedTestHistory speedTests,
        IHistoryMaintenance history,
        string dataFolder,
        params IDisposable[] owned)
    {
        Pinger = pinger;
        Time = time;
        SpeedEndpoint = speedEndpoint;
        Connections = connections;
        PublicIp = publicIp;
        Releases = releases;
        SettingsStore = settingsStore;
        PingRuns = pingRuns;
        SpeedTests = speedTests;
        History = history;
        DataFolder = dataFolder;
        this.owned = owned;
    }

    public IPingSender Pinger { get; }

    public TimeProvider Time { get; }

    public IThroughputEndpoint SpeedEndpoint { get; }

    public IConnectionInfoSource Connections { get; }

    public IPublicIpSource PublicIp { get; }

    public IReleaseSource Releases { get; }

    public ISettingsStore SettingsStore { get; }

    public IPingRunHistory PingRuns { get; }

    public ISpeedTestHistory SpeedTests { get; }

    public IHistoryMaintenance History { get; }

    public string DataFolder { get; }

    /// <summary>The real adapters, with settings in this build's own data folder.</summary>
    public static AppAdapters ForUser(BuildInfo build)
    {
        ArgumentNullException.ThrowIfNull(build);
        var paths = AppDataPaths.ForUser(build.IsDevBuild);
        var userAgent = $"PingRunner/{build.Version}";

        var web = new HttpClient(new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All }) { Timeout = TimeSpan.FromSeconds(15) };
        web.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);

        // Speed tests stream for as long as the test runs; cancellation ends them, not a timeout.
        var speed = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) }) { Timeout = Timeout.InfiniteTimeSpan };
        speed.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);

        // A history written by a newer Ping Runner is left alone; the app runs without one and says why.
        SqliteHistoryDatabase? database = null;
        IPingRunHistory pingRuns;
        ISpeedTestHistory speedTests;
        IHistoryMaintenance history;
        try
        {
            database = SqliteHistoryDatabase.Open(paths.History);
            (pingRuns, speedTests, history) = (new SqlitePingRunHistory(database), new SqliteSpeedTestHistory(database), database);
        }
        catch (HistoryException exception)
        {
            var unavailable = new UnavailableHistory(paths.History, exception.Message);
            (pingRuns, speedTests, history) = (unavailable, unavailable, unavailable);
        }

        return new AppAdapters(
            new IcmpPingSender(),
            TimeProvider.System,
            new CloudflareSpeedEndpoint(speed),
            new SystemConnectionInfoSource(),
            new PublicIpService(web),
            new GitHubReleaseSource(web, "lukr-99", "ping-runner", userAgent),
            new JsonSettingsStore(paths.Settings),
            pingRuns,
            speedTests,
            history,
            paths.Root,
            [web, speed, .. database is null ? Array.Empty<IDisposable>() : [database]]);
    }

    public void Dispose()
    {
        foreach (var disposable in owned)
        {
            disposable.Dispose();
        }
    }
}
