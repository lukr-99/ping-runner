using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PingRunner.App.Composition;
using PingRunner.App.Desktop;
using PingRunner.App.Formatting;
using PingRunner.App.Shell;
using PingRunner.Core.History;
using PingRunner.Core.Sessions;

namespace PingRunner.App.ViewModels;

/// <summary>
/// The History page: every stored ping run and speed test, newest first. A run opens in the Graph,
/// exports to CSV or is deleted; a speed test can be deleted. Deleting asks first. The lists reload
/// whenever the history changes.
/// </summary>
public sealed partial class HistoryViewModel : ObservableObject
{
    private const string CsvFilter = "CSV files (*.csv)|*.csv";

    private readonly IPingRunHistory runs;
    private readonly ISpeedTestHistory speedTests;
    private readonly IHistoryMaintenance maintenance;
    private readonly GraphViewModel graph;
    private readonly ShellNavigation navigation;
    private readonly IDesktopServices desktop;
    private readonly HistoryChanges changes;
    private bool loaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblem))]
    private string? problem;

    [ObservableProperty]
    private string summary = "Loading…";

    [ObservableProperty]
    private bool isLoading;

    public HistoryViewModel(
        IPingRunHistory runs,
        ISpeedTestHistory speedTests,
        IHistoryMaintenance maintenance,
        GraphViewModel graph,
        ShellNavigation navigation,
        IDesktopServices desktop,
        HistoryChanges changes)
    {
        this.runs = runs;
        this.speedTests = speedTests;
        this.maintenance = maintenance;
        this.graph = graph;
        this.navigation = navigation;
        this.desktop = desktop;
        this.changes = changes;
        problem = maintenance.Problem;
        changes.Changed += async (_, _) =>
        {
            if (loaded)
            {
                await RefreshAsync().ConfigureAwait(true);
            }
        };
    }

    public ObservableCollection<RunRowViewModel> Runs { get; } = [];

    public ObservableCollection<SpeedTestResultViewModel> SpeedTests { get; } = [];

    public bool HasProblem => Problem is not null;

    /// <summary>Loads the lists the first time the page opens.</summary>
    public async Task EnsureLoadedAsync()
    {
        if (!loaded)
        {
            loaded = true;
            await RefreshAsync().ConfigureAwait(true);
        }
    }

    /// <summary>For the recorder's failures: the History page says what went wrong.</summary>
    public void ReportProblem(string message) => Problem = message;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            var storedRuns = await runs.ListRunsAsync(CancellationToken.None).ConfigureAwait(true);
            var storedTests = await speedTests.ListAsync(CancellationToken.None).ConfigureAwait(true);
            var info = await maintenance.GetInfoAsync(CancellationToken.None).ConfigureAwait(true);

            Runs.Clear();
            foreach (var run in storedRuns)
            {
                Runs.Add(new RunRowViewModel(run));
            }

            SpeedTests.Clear();
            foreach (var test in storedTests)
            {
                SpeedTests.Add(new SpeedTestResultViewModel(test.Result, test.Id));
            }

            Summary = $"{Units.CountOf(info.Runs, "ping run", "ping runs")} ({Units.CountOf(info.Attempts, "ping", "pings")}) and {Units.CountOf(info.SpeedTests, "speed test", "speed tests")} · {Units.Bytes(info.SizeBytes)}";
        }
        catch (HistoryException exception)
        {
            Problem = exception.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task OpenRunAsync(RunRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        try
        {
            var attempts = await runs.LoadAttemptsAsync(row.Id, CancellationToken.None).ConfigureAwait(true);
            graph.ShowAttempts(attempts, $"History · {row.Target} · {row.When}");
            navigation.Open(AppPage.Graph);
        }
        catch (HistoryException exception)
        {
            desktop.ShowError("Could not open the run", exception.Message);
        }
    }

    [RelayCommand]
    private async Task ExportRunAsync(RunRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        var name = $"{string.Concat(row.Target.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '-' : character))}-run-{row.Id.ToString(CultureInfo.InvariantCulture)}.csv";
        if (desktop.PickFileToSave("Export ping run", CsvFilter, name) is not { } path)
        {
            return;
        }

        try
        {
            var attempts = await runs.LoadAttemptsAsync(row.Id, CancellationToken.None).ConfigureAwait(true);
            var stream = File.Create(path);
            await using (stream.ConfigureAwait(true))
            {
                await PingAttemptCsv.ExportAsync(attempts, stream).ConfigureAwait(true);
            }
        }
        catch (Exception exception) when (exception is HistoryException or IOException or UnauthorizedAccessException)
        {
            desktop.ShowError("Export failed", exception.Message);
        }
    }

    [RelayCommand]
    private async Task DeleteRunAsync(RunRowViewModel? row)
    {
        if (row is null || row.IsRunning
            || !desktop.Confirm("Delete this run?", $"The run to {row.Target} from {row.When} and its {row.Pings} pings will be deleted for good."))
        {
            return;
        }

        try
        {
            await runs.DeleteRunAsync(row.Id, CancellationToken.None).ConfigureAwait(true);
            changes.Raise();
        }
        catch (HistoryException exception)
        {
            desktop.ShowError("Could not delete the run", exception.Message);
        }
    }

    [RelayCommand]
    private async Task DeleteSpeedTestAsync(SpeedTestResultViewModel? test)
    {
        if (test?.Id is not { } id
            || !desktop.Confirm("Delete this speed test?", $"The speed test from {test.WhenLong} will be deleted for good."))
        {
            return;
        }

        try
        {
            await speedTests.DeleteAsync(id, CancellationToken.None).ConfigureAwait(true);
            changes.Raise();
        }
        catch (HistoryException exception)
        {
            desktop.ShowError("Could not delete the speed test", exception.Message);
        }
    }
}
