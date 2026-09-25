using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PingRunner.App.Composition;
using PingRunner.App.Desktop;
using PingRunner.App.Shell;
using PingRunner.Core.Formatting;
using PingRunner.Core.History;
using PingRunner.Core.Importing;
using PingRunner.Core.Sessions;
using PingRunner.Core.Statistics;

namespace PingRunner.App.ViewModels;

/// <summary>
/// The History page: every stored ping run and speed test, newest first. A run opens in the Graph,
/// goes into a report, exports to CSV or is deleted; a speed test can be deleted. Deleting asks first.
/// Ping files (CSV or Excel) import as runs, one per target. The lists reload whenever the history
/// changes.
/// </summary>
public sealed partial class HistoryViewModel : ObservableObject
{
    private const string CsvFilter = "CSV files (*.csv)|*.csv";

    private readonly IPingRunHistory runs;
    private readonly ISpeedTestHistory speedTests;
    private readonly IHistoryMaintenance maintenance;
    private readonly PingFileImporter importer;
    private readonly GraphViewModel graph;
    private readonly ReportsViewModel reports;
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotice))]
    private string? notice;

    public HistoryViewModel(
        IPingRunHistory runs,
        ISpeedTestHistory speedTests,
        IHistoryMaintenance maintenance,
        PingFileImporter importer,
        GraphViewModel graph,
        ReportsViewModel reports,
        ShellNavigation navigation,
        IDesktopServices desktop,
        HistoryChanges changes)
    {
        this.runs = runs;
        this.speedTests = speedTests;
        this.maintenance = maintenance;
        this.importer = importer;
        this.graph = graph;
        this.reports = reports;
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

    /// <summary>Whether the notice shows; closing it clears it.</summary>
    public bool HasNotice
    {
        get => Notice is not null;
        set
        {
            if (!value)
            {
                Notice = null;
            }
        }
    }

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
    private async Task ReportRunAsync(RunRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        await reports.SelectRunAsync(row.Id).ConfigureAwait(true);
        navigation.Open(AppPage.Reports);
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        if (desktop.PickFileToOpen("Import ping data into the history", importer.FileFilter) is not { } path)
        {
            return;
        }

        try
        {
            PingImport import;
            var stream = File.OpenRead(path);
            await using (stream.ConfigureAwait(true))
            {
                import = await importer.ReadAsync(stream, path, CancellationToken.None).ConfigureAwait(true);
            }

            var targets = import.ByTarget();
            foreach (var attempts in targets)
            {
                await runs.ImportRunAsync(import.SourceName, attempts, RunSummary.From(PingStatistics.From(attempts)), CancellationToken.None).ConfigureAwait(true);
            }

            Notice = $"{import.Describe()} Saved as {(targets.Count == 1 ? "a run" : $"{Units.Count(targets.Count)} runs, one per target")}.";
            changes.Raise();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or HistoryException)
        {
            desktop.ShowError("Import failed", exception.Message);
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
