using PingRunner.Core.Settings;

namespace PingRunner.App.Tests.Fakes;

public sealed class InMemorySettingsStore(AppSettings? initial = null) : ISettingsStore
{
    public AppSettings Stored { get; private set; } = initial ?? AppSettings.Default;

    public int Saves { get; private set; }

    public AppSettings Load() => Stored;

    public void Save(AppSettings settings)
    {
        Stored = settings;
        Saves++;
    }
}
