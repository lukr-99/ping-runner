namespace PingRunner.Infrastructure.Storage;

/// <summary>
/// Where Ping Runner keeps its files: <c>%LOCALAPPDATA%\PingRunner</c> for releases and
/// <c>%LOCALAPPDATA%\PingRunner Dev</c> for -dev builds, so trying a build never touches the installed
/// app's settings. Uninstalling leaves the folder; deleting it resets Ping Runner.
/// </summary>
public sealed class AppDataPaths(string root)
{
    public string Root { get; } = root;

    public string Settings => Path.Combine(Root, "settings.json");

    public string History => Path.Combine(Root, "history.db");

    public static AppDataPaths ForUser(bool isDevBuild) => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        isDevBuild ? "PingRunner Dev" : "PingRunner"));
}
