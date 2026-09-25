using System.Windows;
using System.Windows.Media;
using PingRunner.App.Tests.Hosting;
using PingRunner.Core.Settings;
using PingRunner.Core.Updates;
using ThemeMode = PingRunner.Core.Settings.ThemeMode;

namespace PingRunner.App.Tests.ViewModels;

[Collection(WpfCollection.Name)]
public sealed class SettingsViewModelTests
{
    [Fact]
    public Task SelectingDarkAndViolet_SavesAndRecolorsAtOnce() => WpfHost.RunAsync(() =>
    {
        using var app = TestApp.Create();
        var settings = app.Graph.SettingsPage;

        settings.SelectedTheme = settings.Themes.Single(choice => choice.Value == ThemeMode.Dark);
        settings.SelectedAccent = settings.Accents.Single(choice => choice.Value == AccentChoice.Violet);

        Assert.Equal(ThemeMode.Dark, app.Store.Stored.Theme);
        Assert.Equal(AccentChoice.Violet, app.Store.Stored.Accent);
        Assert.True(app.Graph.Theme.IsDark);
        var accent = (SolidColorBrush)Application.Current.Resources["PR.AccentBrush"];
        Assert.Equal(Color.FromRgb(0xA7, 0x8B, 0xFA), accent.Color);
        app.Graph.Theme.Apply(ThemeMode.Light, AccentChoice.Teal);
        return Task.CompletedTask;
    });

    [Fact]
    public Task Capacity_AppliesToTheSession() => WpfHost.RunAsync(() =>
    {
        using var app = TestApp.Create();
        var settings = app.Graph.SettingsPage;

        settings.SelectedCapacity = settings.Capacities[0];

        Assert.Equal(10_000, app.Graph.Session.Capacity);
        Assert.Equal(10_000, app.Store.Stored.MaximumStoredAttempts);
        return Task.CompletedTask;
    });

    [Fact]
    public Task CheckForUpdates_NewerRelease_OffersItsPage() => WpfHost.RunAsync(async () =>
    {
        var release = new ReleaseInfo(ReleaseVersion.TryParse("2.1.0")!, "Ping Runner 2.1.0", new Uri("https://github.com/lukr-99/ping-runner/releases/tag/v2.1.0"), null);
        using var app = TestApp.Create(release: release);
        var settings = app.Graph.SettingsPage;

        await settings.CheckForUpdatesCommand.ExecuteAsync(null);
        settings.OpenReleaseCommand.Execute(null);

        Assert.True(settings.HasRelease);
        Assert.Equal("Ping Runner 2.1.0 is available.", settings.UpdateText);
        Assert.Equal(["https://github.com/lukr-99/ping-runner/releases/tag/v2.1.0"], app.Desktop.Opened);
    });

    [Fact]
    public Task CheckForUpdates_NothingPublished_SaysSo() => WpfHost.RunAsync(async () =>
    {
        using var app = TestApp.Create();

        await app.Graph.SettingsPage.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.False(app.Graph.SettingsPage.HasRelease);
        Assert.Equal("No release has been published yet.", app.Graph.SettingsPage.UpdateText);
    });
}
