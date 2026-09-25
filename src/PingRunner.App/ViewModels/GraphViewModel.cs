using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PingRunner.App.Desktop;
using PingRunner.Core.Formatting;
using PingRunner.Core.Graphing;
using PingRunner.Core.Pinging;
using PingRunner.Core.Sessions;
using PingRunner.Core.Statistics;

namespace PingRunner.App.ViewModels;

/// <summary>
/// The Graph page: the live session, a stored run or an imported CSV, narrowed to a range, then zoomed and panned
/// on the chart. The statistics strip always describes exactly what is on screen, and exports can
/// take either the visible part or the whole source.
/// </summary>
public sealed partial class GraphViewModel : ObservableObject
{
    private const string CsvFilter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";

    private readonly PingSession session;
    private readonly IDesktopServices desktop;
    private IReadOnlyList<PingAttempt>? imported;
    private string? importedLabel;
    private bool dirty = true;

    [ObservableProperty]
    private GraphRange selectedRange = GraphRange.Presets[1];

    [ObservableProperty]
    private GraphZoom zoom = GraphZoom.None;

    [ObservableProperty]
    private IReadOnlyList<PingAttempt> rangedAttempts = [];

    [ObservableProperty]
    private StatisticsDisplay stats = StatisticsDisplay.Empty;

    [ObservableProperty]
    private string sourceText = "Live session";

    [ObservableProperty]
    private string viewText = "No pings yet";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UseLiveDataCommand))]
    private bool isImported;

    public GraphViewModel(PingSession session, IDesktopServices desktop)
    {
        this.session = session;
        this.desktop = desktop;
        session.AttemptRecorded += (_, _) => dirty = true;
        session.StateChanged += (_, _) => dirty = true;
    }

    public IReadOnlyList<GraphRange> Ranges => GraphRange.Presets;

    /// <summary>Rebuilds the view when the live session changed since the last call.</summary>
    public void Refresh()
    {
        if (!dirty)
        {
            return;
        }

        dirty = false;
        Rebuild();
    }

    /// <summary>A file name for an export: the target, what is exported, and the time.</summary>
    public string SuggestedFileName(string what, string extension)
    {
        var source = Source();
        var host = source.Select(attempt => attempt.TargetHost).Distinct(StringComparer.OrdinalIgnoreCase).Take(2).ToList() switch
        {
            [var single] => single,
            [] => "ping-session",
            _ => "multiple-targets",
        };
        var safe = string.Concat(host.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '-' : character));
        return $"{safe}-{what}-{DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.{extension}";
    }

    /// <summary>Asks where to save a PNG, then lets the view render into it.</summary>
    public void ExportImage(Action<string> render)
    {
        ArgumentNullException.ThrowIfNull(render);
        if (desktop.PickFileToSave("Export graph", "PNG image (*.png)|*.png", SuggestedFileName("graph", "png")) is not { } path)
        {
            return;
        }

        try
        {
            render(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            desktop.ShowError("PNG export failed", exception.Message);
        }
    }

    public async Task ImportFromAsync(string path)
    {
        try
        {
            IReadOnlyList<PingAttempt> attempts;
            var stream = File.OpenRead(path);
            await using (stream.ConfigureAwait(true))
            {
                attempts = await PingAttemptCsv.ImportAsync(stream).ConfigureAwait(true);
            }

            ShowAttempts(attempts, $"Imported · {Path.GetFileName(path)}");
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            desktop.ShowError("Import failed", exception.Message);
        }
    }

    /// <summary>Shows attempts from somewhere other than the live session (a file, the history) until "Back to live data".</summary>
    public void ShowAttempts(IReadOnlyList<PingAttempt> attempts, string label)
    {
        ArgumentNullException.ThrowIfNull(attempts);
        imported = attempts;
        importedLabel = label;
        IsImported = true;
        Zoom = GraphZoom.None;
        if (SelectedRange == GraphRange.Everything)
        {
            Rebuild();
        }
        else
        {
            SelectedRange = GraphRange.Everything;
        }
    }

    partial void OnSelectedRangeChanged(GraphRange value)
    {
        Zoom = GraphZoom.None;
        Rebuild();
    }

    partial void OnZoomChanged(GraphZoom value) => UpdateVisibleStatistics();

    [RelayCommand]
    private async Task ImportAsync()
    {
        if (desktop.PickFileToOpen("Import ping data", CsvFilter) is { } path)
        {
            await ImportFromAsync(path).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private Task ExportVisibleAsync() => ExportAsync(Zoom.Apply(RangedAttempts), "view");

    [RelayCommand]
    private Task ExportAllAsync() => ExportAsync(Source(), "all");

    [RelayCommand(CanExecute = nameof(IsImported))]
    private void UseLiveData()
    {
        imported = null;
        importedLabel = null;
        IsImported = false;
        Zoom = GraphZoom.None;
        Rebuild();
    }

    [RelayCommand]
    private void ResetZoom() => Zoom = GraphZoom.None;

    private IReadOnlyList<PingAttempt> Source() => imported ?? session.Snapshot();

    private void Rebuild()
    {
        var source = Source();
        RangedAttempts = SelectedRange.Apply(source);
        SourceText = imported is null
            ? session.Settings is { } run ? $"Live session · {run.TargetHost}" : "Live session"
            : importedLabel ?? "Imported";
        UpdateVisibleStatistics();
    }

    private void UpdateVisibleStatistics()
    {
        var visible = Zoom.Apply(RangedAttempts);
        Stats = StatisticsDisplay.From(PingStatistics.From(visible));
        var total = imported?.Count ?? session.Attempts.Count;
        ViewText = visible.Count == 0
            ? imported is null ? "No pings yet. Start a run on the Monitor page, open one from History, or import a CSV." : "There are no pings to show."
            : $"Showing {Units.Count(visible.Count)} of {Units.Count(total)} pings · {visible[0].Timestamp.ToLocalTime():HH:mm:ss} – {visible[^1].Timestamp.ToLocalTime():HH:mm:ss}";
    }

    private async Task ExportAsync(IReadOnlyList<PingAttempt> attempts, string what)
    {
        if (desktop.PickFileToSave("Export ping data", "CSV files (*.csv)|*.csv", SuggestedFileName(what, "csv")) is not { } path)
        {
            return;
        }

        try
        {
            var stream = File.Create(path);
            await using (stream.ConfigureAwait(true))
            {
                await PingAttemptCsv.ExportAsync(attempts, stream).ConfigureAwait(true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            desktop.ShowError("Export failed", exception.Message);
        }
    }
}
