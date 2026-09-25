using PingRunner.App.Shell;
using PingRunner.App.Tests.Hosting;

namespace PingRunner.App.Tests.ViewModels;

[Collection(WpfCollection.Name)]
public sealed class HistoryViewModelTests
{
    [Fact]
    public Task StoppedRun_IsListedWithItsFigures() => WpfHost.RunAsync(async () =>
    {
        using var app = TestApp.Create();
        await app.PingAsync(30);
        app.Graph.Monitor.StopCommand.Execute(null);
        await app.AdvanceUntilAsync(() => !app.Graph.Monitor.IsRunning, TimeSpan.FromMilliseconds(100));
        await app.Graph.Recorder.WhenIdleAsync();

        await app.Graph.History.EnsureLoadedAsync();

        var row = Assert.Single(app.Graph.History.Runs);
        Assert.Equal("8.8.8.8", row.Target);
        Assert.Equal("Stopped", row.Status);
        Assert.Equal(app.Graph.Session.Attempts.Count.ToString(System.Globalization.CultureInfo.CurrentCulture), row.Pings);
        Assert.Equal(app.Graph.Session.Attempts.Count, app.Runs.StoredAttempts(row.Id));
    });

    [Fact]
    public Task OpenRun_ShowsItInTheGraph() => WpfHost.RunAsync(async () =>
    {
        using var app = TestApp.Create();
        await app.SeedHistoryAsync();
        var opened = new List<AppPage>();
        app.Graph.Navigation.Requested += (_, page) => opened.Add(page);
        await app.Graph.History.EnsureLoadedAsync();
        var run = app.Graph.History.Runs.First(row => row.Target == "1.1.1.1");

        await app.Graph.History.OpenRunCommand.ExecuteAsync(run);

        Assert.Equal([AppPage.Graph], opened);
        Assert.True(app.Graph.Graph.IsImported);
        Assert.StartsWith("History · 1.1.1.1", app.Graph.Graph.SourceText, StringComparison.Ordinal);
        Assert.Equal(900, app.Graph.Graph.RangedAttempts.Count);
    });

    [Fact]
    public Task DeleteRun_OnlyAfterConfirming() => WpfHost.RunAsync(async () =>
    {
        using var app = TestApp.Create();
        await app.SeedHistoryAsync();
        var history = app.Graph.History;
        await history.EnsureLoadedAsync();
        var run = history.Runs[0];

        app.Desktop.ConfirmAnswer = false;
        await history.DeleteRunCommand.ExecuteAsync(run);
        Assert.Equal(3, (await app.Runs.ListRunsAsync(CancellationToken.None)).Count);

        app.Desktop.ConfirmAnswer = true;
        await history.DeleteRunCommand.ExecuteAsync(run);
        await Task.Yield();

        Assert.Equal(2, (await app.Runs.ListRunsAsync(CancellationToken.None)).Count);
        Assert.Equal(2, app.Desktop.Confirmations.Count);
    });

    [Fact]
    public Task SpeedTest_IsSavedAndListed() => WpfHost.RunAsync(async () =>
    {
        using var app = TestApp.Create(Core.Settings.AppSettings.Default with { SpeedTestSeconds = 3 });

        await app.RunSpeedTestAsync();
        await app.Graph.History.EnsureLoadedAsync();

        Assert.Equal(1, app.SpeedTests.Count);
        var listed = Assert.Single(app.Graph.History.SpeedTests);
        Assert.NotNull(listed.Id);
        Assert.Equal(listed.Id, app.Graph.SpeedTest.History[0].Id);
    });

    [Fact]
    public Task SpeedTestPage_ShowsTheLatestStoredTestAtStart() => WpfHost.RunAsync(async () =>
    {
        using var first = TestApp.Create(Core.Settings.AppSettings.Default with { SpeedTestSeconds = 3 });
        await first.RunSpeedTestAsync();
        var stored = (await first.SpeedTests.ListAsync(CancellationToken.None))[0];

        using var app = TestApp.Create();
        await app.SpeedTests.SaveAsync(stored.Result, CancellationToken.None);
        await app.Graph.SpeedTest.LoadAsync();

        Assert.NotNull(app.Graph.SpeedTest.Latest);
        Assert.StartsWith("Last test:", app.Graph.SpeedTest.PhaseText, StringComparison.Ordinal);
    });
}
