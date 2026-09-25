using PingRunner.App.Tests.Hosting;
using PingRunner.App.ViewModels;

namespace PingRunner.App.Tests.ViewModels;

[Collection(WpfCollection.Name)]
public sealed class MonitorViewModelTests
{
    [Fact]
    public Task Start_IntervalNotANumber_ExplainsAndDoesNotRun() => WpfHost.RunAsync(async () =>
    {
        using var app = TestApp.Create();
        var monitor = app.Graph.Monitor;
        monitor.IntervalText = "fast";

        await monitor.StartCommand.ExecuteAsync(null);

        Assert.True(monitor.HasError);
        Assert.False(app.Graph.Session.IsRunning);
    });

    [Fact]
    public Task Start_InvalidHost_ShowsTheSettingsError() => WpfHost.RunAsync(async () =>
    {
        using var app = TestApp.Create();
        var monitor = app.Graph.Monitor;
        monitor.Target = "two words";

        await monitor.StartCommand.ExecuteAsync(null);

        Assert.Equal("A host name cannot contain spaces.", monitor.Error);
    });

    [Fact]
    public Task Start_ValidForm_RunsAndFillsTheStatistics() => WpfHost.RunAsync(async () =>
    {
        using var app = TestApp.Create();
        var monitor = app.Graph.Monitor;
        monitor.Target = "1.1.1.1";

        await app.PingAsync(30);
        var sent = app.Graph.Session.Attempts.Count;

        Assert.True(monitor.IsRunning);
        Assert.Equal("Pinging 1.1.1.1", monitor.StatusText);
        Assert.Equal(sent.ToString(System.Globalization.CultureInfo.CurrentCulture), monitor.Stats.Sent);
        Assert.Equal(sent, monitor.RecentAttempts.Count);
        Assert.Equal(sent, monitor.ChartAttempts.Count);
        Assert.Equal("1.1.1.1", app.Store.Stored.RecentTargets[0]);
    });

    [Fact]
    public Task Stop_EndsTheRunAndSaysSo() => WpfHost.RunAsync(async () =>
    {
        using var app = TestApp.Create();
        await app.PingAsync(5);

        app.Graph.Monitor.StopCommand.Execute(null);
        await app.AdvanceUntilAsync(() => !app.Graph.Monitor.IsRunning, TimeSpan.FromMilliseconds(100));

        Assert.Equal("Stopped 8.8.8.8", app.Graph.Monitor.StatusText);
    });

    [Fact]
    public Task CopySummary_CopiesTheSessionFigures() => WpfHost.RunAsync(async () =>
    {
        using var app = TestApp.Create();
        await app.PingAsync(10);

        app.Graph.Monitor.CopySummaryCommand.Execute(null);

        var summary = Assert.Single(app.Desktop.Copied);
        Assert.Contains("8.8.8.8", summary, StringComparison.Ordinal);
        Assert.Contains($"Sent {app.Graph.Session.Attempts.Count}", summary, StringComparison.Ordinal);
        Assert.Contains("Jitter", summary, StringComparison.Ordinal);
    });

    [Fact]
    public Task UseTarget_WhileIdle_FillsTheForm() => WpfHost.RunAsync(() =>
    {
        using var app = TestApp.Create();

        app.Graph.Monitor.UseTarget("192.168.1.1");

        Assert.Equal("192.168.1.1", app.Graph.Monitor.Target);
        return Task.CompletedTask;
    });

    [Fact]
    public void DurationOption_UnknownLength_GetsItsOwnLabel()
    {
        Assert.Same(DurationOption.Presets[0], DurationOption.For(null));
        Assert.Equal("2 min 00 s", DurationOption.For(TimeSpan.FromMinutes(2)).Label);
    }
}
