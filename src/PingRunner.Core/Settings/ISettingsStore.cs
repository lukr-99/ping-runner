namespace PingRunner.Core.Settings;

/// <summary>Where the settings live between runs. A missing or unreadable file loads the defaults.</summary>
public interface ISettingsStore
{
    AppSettings Load();

    void Save(AppSettings settings);
}
