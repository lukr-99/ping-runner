using CommunityToolkit.Mvvm.Input;
using PingRunner.App.Composition;
using PingRunner.App.Desktop;
using PingRunner.Core.About;

namespace PingRunner.App.ViewModels;

/// <summary>
/// The About page: this build's version and kind next to the text and links in
/// <see cref="AboutContent"/>. A link opens in the browser only after a click.
/// </summary>
public sealed partial class AboutViewModel(AboutContent content, BuildInfo build, string dataFolder, IDesktopServices desktop)
{
    public AboutContent Content { get; } = content;

    public string Version { get; } = build.Version.ToString();

    public string BuildLabel { get; } = AboutContent.BuildLabel(build.Version.ToString(), build.IsReleaseBuild);

    public string DataFolder { get; } = dataFolder;

    [RelayCommand]
    private void OpenLink(string? url)
    {
        if (!string.IsNullOrEmpty(url))
        {
            desktop.Open(url);
        }
    }

    [RelayCommand]
    private void OpenDataFolder() => desktop.Open(DataFolder);
}
