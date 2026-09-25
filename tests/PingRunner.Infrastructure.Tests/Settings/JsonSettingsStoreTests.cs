using PingRunner.Core.Settings;
using PingRunner.Infrastructure.Settings;
using PingRunner.Infrastructure.Tests.Fakes;

namespace PingRunner.Infrastructure.Tests.Settings;

public sealed class JsonSettingsStoreTests
{
    [Fact]
    public void Load_NoFile_ReturnsDefaults()
    {
        using var folder = new TemporaryDirectory();

        Assert.Equal(AppSettings.Default, new JsonSettingsStore(folder.File("settings.json")).Load());
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        using var folder = new TemporaryDirectory();
        var store = new JsonSettingsStore(folder.File("nested/settings.json"));
        var settings = AppSettings.Default.WithRecentTarget("192.168.1.1") with
        {
            Theme = ThemeMode.Dark,
            Accent = AccentChoice.Violet,
            IntervalMilliseconds = 250,
            RunUntilStopped = false,
        };

        store.Save(settings);
        var loaded = store.Load();

        Assert.Equal(settings.Theme, loaded.Theme);
        Assert.Equal(settings.Accent, loaded.Accent);
        Assert.Equal(settings.Target, loaded.Target);
        Assert.Equal(settings.RecentTargets, loaded.RecentTargets);
        Assert.Equal(250, loaded.IntervalMilliseconds);
        Assert.False(loaded.RunUntilStopped);
        Assert.False(File.Exists(folder.File("nested/settings.json.tmp")));
    }

    [Fact]
    public void Save_WritesEnumsAsNamesAndAVersion()
    {
        using var folder = new TemporaryDirectory();
        var path = folder.File("settings.json");

        new JsonSettingsStore(path).Save(AppSettings.Default with { Theme = ThemeMode.Light });

        var json = File.ReadAllText(path);
        Assert.Contains("\"Theme\": \"Light\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Version\": 1", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_CorruptFile_KeepsACopyAndReturnsDefaults()
    {
        using var folder = new TemporaryDirectory();
        var path = folder.File("settings.json");
        File.WriteAllText(path, "{ not json");

        var loaded = new JsonSettingsStore(path).Load();

        Assert.Equal(AppSettings.Default, loaded);
        Assert.Equal("{ not json", File.ReadAllText(folder.File("settings.unreadable.json")));
    }

    [Fact]
    public void Load_OutOfRangeValues_AreNormalized()
    {
        using var folder = new TemporaryDirectory();
        var path = folder.File("settings.json");
        File.WriteAllText(path, """{ "Version": 1, "IntervalMilliseconds": 5, "Theme": "Dark", "Unknown": true }""");

        var loaded = new JsonSettingsStore(path).Load();

        Assert.Equal(100, loaded.IntervalMilliseconds);
        Assert.Equal(ThemeMode.Dark, loaded.Theme);
    }
}
