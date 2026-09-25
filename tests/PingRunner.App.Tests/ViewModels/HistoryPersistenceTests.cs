using System.IO;
using PingRunner.App.Tests.Hosting;

namespace PingRunner.App.Tests.ViewModels;

/// <summary>The whole app on a real history file: what it measured is still there after a restart.</summary>
[Collection(WpfCollection.Name)]
public sealed class HistoryPersistenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 9, 0, 0, TimeSpan.FromHours(2));

    [Fact]
    public Task RunAndSpeedTest_AreStillThereAfterARestart() => WpfHost.RunAsync(async () =>
    {
        var folder = Path.Combine(Path.GetTempPath(), "PingRunnerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "history.db");
        try
        {
            int sent;
            using (var first = SqliteTestApp.Open(path, Now))
            {
                _ = first.Graph.Monitor.StartCommand.ExecuteAsync(null);
                await first.AdvanceUntilAsync(() => first.Graph.Session.Attempts.Count >= 40, TimeSpan.FromSeconds(1));
                first.Graph.Monitor.StopCommand.Execute(null);
                await first.AdvanceUntilAsync(() => !first.Graph.Monitor.IsRunning, TimeSpan.FromMilliseconds(100));
                sent = first.Graph.Session.Attempts.Count;

                var speed = first.Graph.SpeedTest.RunCommand.ExecuteAsync(null);
                await first.AdvanceUntilAsync(() => speed.IsCompleted, TimeSpan.FromMilliseconds(10));
                await speed;
                await first.Graph.Recorder.WhenIdleAsync();
            }

            using var second = SqliteTestApp.Open(path, Now.AddHours(1));
            await second.Graph.History.EnsureLoadedAsync();
            await second.Graph.SpeedTest.LoadAsync();

            var run = Assert.Single(second.Graph.History.Runs);
            Assert.Equal("Stopped", run.Status);
            Assert.Equal(sent.ToString(System.Globalization.CultureInfo.CurrentCulture), run.Pings);
            Assert.Single(second.Graph.History.SpeedTests);
            Assert.NotNull(second.Graph.SpeedTest.Latest);
            Assert.Null(second.Graph.History.Problem);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    });

    [Fact]
    public Task RunLeftOpenByACrash_IsClosedAsInterruptedAtTheNextStart() => WpfHost.RunAsync(async () =>
    {
        var folder = Path.Combine(Path.GetTempPath(), "PingRunnerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "history.db");
        try
        {
            // A "crash": the app never gets to finish the run, only the batches already written survive.
            var crashed = SqliteTestApp.Open(path, Now);
            _ = crashed.Graph.Monitor.StartCommand.ExecuteAsync(null);
            await crashed.AdvanceUntilAsync(() => crashed.Graph.Session.Attempts.Count >= 30, TimeSpan.FromSeconds(1));
            await crashed.Graph.Recorder.WhenIdleAsync();
            crashed.Graph.Recorder.Dispose();
            crashed.Graph.Session.Stop();

            using var restarted = SqliteTestApp.Open(path, Now.AddHours(1));
            await restarted.Graph.Recorder.WhenIdleAsync();
            await restarted.Graph.History.EnsureLoadedAsync();

            var run = Assert.Single(restarted.Graph.History.Runs);
            Assert.Equal("Interrupted", run.Status);
            Assert.NotEqual("0", run.Pings);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    });
}
