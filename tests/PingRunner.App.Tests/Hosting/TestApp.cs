using System.Windows;
using Microsoft.Extensions.Time.Testing;
using PingRunner.App.Composition;
using PingRunner.App.Tests.Fakes;
using PingRunner.Core.Settings;
using PingRunner.Core.Updates;

namespace PingRunner.App.Tests.Hosting;

/// <summary>
/// The whole app graph on fakes and a fake clock, built on the WPF host thread. Helpers move the
/// clock forward until a run reaches the wanted state, yielding to the dispatcher in between so
/// continuations posted to it can run.
/// </summary>
public sealed class TestApp : IDisposable
{
    private TestApp(AppGraph graph, FakeTimeProvider time, RecordingDesktopServices desktop, InMemorySettingsStore store)
    {
        Graph = graph;
        Time = time;
        Desktop = desktop;
        Store = store;
    }

    public AppGraph Graph { get; }

    public FakeTimeProvider Time { get; }

    public RecordingDesktopServices Desktop { get; }

    public InMemorySettingsStore Store { get; }

    /// <summary>Must run on the WPF host thread.</summary>
    public static TestApp Create(AppSettings? settings = null, ReleaseInfo? release = null, bool releaseBuild = true)
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 25, 18, 30, 0, TimeSpan.Zero));
        var desktop = new RecordingDesktopServices();
        var store = new InMemorySettingsStore(settings);
        var adapters = new AppAdapters(
            new RealisticPingSender(),
            time,
            new ClockedSpeedEndpoint(time),
            new FixedConnectionInfoSource(FixedConnectionInfoSource.HomeWiFi),
            new FixedPublicIpSource("203.0.113.42"),
            new FixedReleaseSource(release),
            store,
            @"C:\Users\you\AppData\Local\PingRunner");
        var build = new BuildInfo(ReleaseVersion.TryParse(releaseBuild ? "2.0.0" : "2.0.0-dev")!, releaseBuild);
        var graph = new AppGraph(build, adapters, Application.Current.Resources, desktop);
        return new TestApp(graph, time, desktop, store);
    }

    /// <summary>Starts a run from the Monitor form and moves the clock until it has sent <paramref name="count"/> pings.</summary>
    public async Task PingAsync(int count)
    {
        _ = Graph.Monitor.StartCommand.ExecuteAsync(null);
        await AdvanceUntilAsync(() => Graph.Session.Attempts.Count >= count, TimeSpan.FromSeconds(1));
        Graph.RefreshPages();
    }

    public async Task RunSpeedTestAsync()
    {
        var run = Graph.SpeedTest.RunCommand.ExecuteAsync(null);
        await AdvanceUntilAsync(() => run.IsCompleted, TimeSpan.FromMilliseconds(10));
        await run;
    }

    public async Task AdvanceUntilAsync(Func<bool> done, TimeSpan step)
    {
        for (var guard = 0; guard < 200_000; guard++)
        {
            if (done())
            {
                return;
            }

            Time.Advance(step);
            await Task.Yield();
        }

        throw new TimeoutException("The fake clock ran out before the app got there.");
    }

    public void Dispose()
    {
        Graph.Session.Stop();
        Graph.Dispose();
    }
}
