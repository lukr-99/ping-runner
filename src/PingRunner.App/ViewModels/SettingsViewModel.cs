using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PingRunner.App.Composition;
using PingRunner.App.Desktop;
using PingRunner.App.Formatting;
using PingRunner.App.Theming;
using PingRunner.Core.Sessions;
using PingRunner.Core.Settings;
using PingRunner.Core.Updates;

namespace PingRunner.App.ViewModels;

/// <summary>
/// The Settings page: theme and accent (applied at once), how much of a session to keep, speed-test
/// length and streams, the update check and what this build is.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    public const string RepositoryUrl = "https://github.com/lukr-99/ping-runner";

    private readonly SettingsState settings;
    private readonly ThemeApplier theme;
    private readonly PingSession session;
    private readonly UpdateCheck updates;
    private readonly IDesktopServices desktop;
    private Uri? releasePage;

    [ObservableProperty]
    private ChoiceViewModel<ThemeMode> selectedTheme;

    [ObservableProperty]
    private ChoiceViewModel<AccentChoice> selectedAccent;

    [ObservableProperty]
    private ChoiceViewModel<int> selectedCapacity;

    [ObservableProperty]
    private ChoiceViewModel<int> selectedSpeedTestSeconds;

    [ObservableProperty]
    private ChoiceViewModel<int> selectedSpeedTestStreams;

    [ObservableProperty]
    private string updateText = "Not checked yet";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenReleaseCommand))]
    private bool hasRelease;

    [ObservableProperty]
    private bool isChecking;

    public SettingsViewModel(
        SettingsState settings,
        ThemeApplier theme,
        PingSession session,
        UpdateCheck updates,
        BuildInfo build,
        string dataFolder,
        IDesktopServices desktop)
    {
        this.settings = settings;
        this.theme = theme;
        this.session = session;
        this.updates = updates;
        this.desktop = desktop;
        DataFolder = dataFolder;
        Version = build.Version.ToString();
        BuildKind = build.IsReleaseBuild ? "Release build" : "Development build (settings kept apart from the installed app)";

        Themes =
        [
            new(ThemeMode.System, "Use Windows setting"),
            new(ThemeMode.Light, "Light"),
            new(ThemeMode.Dark, "Dark"),
        ];
        Accents = [.. AccentPalette.All.Select(accent => new ChoiceViewModel<AccentChoice>(accent.Choice, accent.Name, Swatch(accent)))];
        Capacities = [.. new[] { 10_000, 50_000, 100_000, 500_000 }.Select(count => new ChoiceViewModel<int>(count, $"{Units.Count(count)} pings"))];
        SpeedTestLengths = [.. new[] { 5, 10, 15, 20, 30 }.Select(seconds => new ChoiceViewModel<int>(seconds, $"{seconds} seconds each way"))];
        SpeedTestStreamCounts = [.. new[] { 1, 2, 4, 8 }.Select(count => new ChoiceViewModel<int>(count, count == 1 ? "1 stream" : $"{count} parallel streams"))];

        var current = settings.Current;
        selectedTheme = Pick(Themes, current.Theme);
        selectedAccent = Pick(Accents, current.Accent);
        selectedCapacity = Pick(Capacities, current.MaximumStoredAttempts);
        selectedSpeedTestSeconds = Pick(SpeedTestLengths, current.SpeedTestSeconds);
        selectedSpeedTestStreams = Pick(SpeedTestStreamCounts, current.SpeedTestStreams);
    }

    public IReadOnlyList<ChoiceViewModel<ThemeMode>> Themes { get; }

    public IReadOnlyList<ChoiceViewModel<AccentChoice>> Accents { get; }

    public IReadOnlyList<ChoiceViewModel<int>> Capacities { get; }

    public IReadOnlyList<ChoiceViewModel<int>> SpeedTestLengths { get; }

    public IReadOnlyList<ChoiceViewModel<int>> SpeedTestStreamCounts { get; }

    public string Version { get; }

    public string BuildKind { get; }

    public string DataFolder { get; }

    partial void OnSelectedThemeChanged(ChoiceViewModel<ThemeMode> value) => ApplyAppearance();

    partial void OnSelectedAccentChanged(ChoiceViewModel<AccentChoice> value) => ApplyAppearance();

    partial void OnSelectedCapacityChanged(ChoiceViewModel<int> value)
    {
        settings.Update(current => current with { MaximumStoredAttempts = value.Value });
        session.Capacity = settings.Current.MaximumStoredAttempts;
    }

    partial void OnSelectedSpeedTestSecondsChanged(ChoiceViewModel<int> value) =>
        settings.Update(current => current with { SpeedTestSeconds = value.Value });

    partial void OnSelectedSpeedTestStreamsChanged(ChoiceViewModel<int> value) =>
        settings.Update(current => current with { SpeedTestStreams = value.Value });

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task CheckForUpdatesAsync()
    {
        IsChecking = true;
        UpdateText = "Checking…";
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var result = await updates.CheckAsync(timeout.Token).ConfigureAwait(true);
            releasePage = result.Release?.PageUrl;
            HasRelease = result.Status == UpdateStatus.UpdateAvailable;
            UpdateText = result.Status switch
            {
                UpdateStatus.UpdateAvailable => $"{result.Release!.Title} is available.",
                UpdateStatus.UpToDate => $"Up to date. The latest release is {result.Release!.Version}.",
                UpdateStatus.NoReleases => "No release has been published yet.",
                _ => $"Could not check: {result.Error}",
            };
        }
        catch (OperationCanceledException)
        {
            UpdateText = "Could not check: GitHub did not answer in time.";
        }
        finally
        {
            IsChecking = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasRelease))]
    private void OpenRelease()
    {
        if (releasePage is not null)
        {
            desktop.Open(releasePage.AbsoluteUri);
        }
    }

    [RelayCommand]
    private void OpenDataFolder() => desktop.Open(DataFolder);

    [RelayCommand]
    private void OpenRepository() => desktop.Open(RepositoryUrl);

    private void ApplyAppearance()
    {
        settings.Update(current => current with { Theme = SelectedTheme.Value, Accent = SelectedAccent.Value });
        theme.Apply(SelectedTheme.Value, SelectedAccent.Value);
    }

    private static ChoiceViewModel<T> Pick<T>(IReadOnlyList<ChoiceViewModel<T>> options, T value) =>
        options.FirstOrDefault(option => EqualityComparer<T>.Default.Equals(option.Value, value)) ?? options[0];

    private static SolidColorBrush Swatch(AccentPalette accent)
    {
        var brush = new SolidColorBrush(accent.OnLight);
        brush.Freeze();
        return brush;
    }
}
