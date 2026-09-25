using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PingRunner.App.Composition;
using PingRunner.App.Desktop;
using PingRunner.App.Reports;
using PingRunner.App.Theming;
using PingRunner.Core.Formatting;
using PingRunner.Core.History;
using PingRunner.Core.Pinging;
using PingRunner.Core.Reports;
using PingRunner.Core.SpeedTest;

namespace PingRunner.App.ViewModels;

/// <summary>
/// The Reports page: pick what to report on (the live session, what the Graph shows, a stored run or
/// a period of the history), label it, choose what goes in, see the figures and findings, then save
/// it as a PDF to hand over or an Excel workbook to work with. Charts are drawn in the app's accent.
/// </summary>
public sealed partial class ReportsViewModel : ObservableObject
{
    private readonly ReportSources sources;
    private readonly IReadOnlyList<IReportWriter> writers;
    private readonly ReportChartRenderer charts;
    private readonly SettingsState settings;
    private readonly TimeProvider time;
    private readonly string appVersion;
    private readonly IDesktopServices desktop;
    private IReadOnlyList<RunRecord> storedRuns = [];
    private IReadOnlyList<SpeedTestResult> storedSpeedTests = [];
    private IReadOnlyList<PingAttempt> selected = [];
    private string subject = string.Empty;
    private int previewVersion;
    private bool loaded;
    private bool quiet;

    [ObservableProperty]
    private ReportSourceKind sourceKind = ReportSourceKind.LiveSession;

    [ObservableProperty]
    private ReportRunChoice? selectedRun;

    [ObservableProperty]
    private string? selectedTarget;

    [ObservableProperty]
    private DateTime periodFrom;

    [ObservableProperty]
    private DateTime periodTo;

    [ObservableProperty]
    private string title = ReportOptions.DefaultTitle;

    [ObservableProperty]
    private string notes = string.Empty;

    [ObservableProperty]
    private bool includeSpeedTests = true;

    [ObservableProperty]
    private bool includeConnection = true;

    [ObservableProperty]
    private bool includePublicIp;

    [ObservableProperty]
    private bool includeAllPings = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreview))]
    [NotifyCanExecuteChangedFor(nameof(SavePdfCommand), nameof(SaveExcelCommand))]
    private int pingCount;

    [ObservableProperty]
    private string previewHeading = string.Empty;

    [ObservableProperty]
    private string previewMessage = "Loading…";

    [ObservableProperty]
    private StatisticsDisplay stats = StatisticsDisplay.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SavePdfCommand), nameof(SaveExcelCommand))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSaved))]
    [NotifyCanExecuteChangedFor(nameof(OpenSavedCommand), nameof(ShowSavedInFolderCommand))]
    private string? savedPath;

    [ObservableProperty]
    private string savedText = string.Empty;

    public ReportsViewModel(
        ReportSources sources,
        IReadOnlyList<IReportWriter> writers,
        ReportChartRenderer charts,
        SettingsState settings,
        TimeProvider time,
        string appVersion,
        IDesktopServices desktop,
        HistoryChanges changes)
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(changes);
        this.sources = sources;
        this.writers = writers;
        this.charts = charts;
        this.settings = settings;
        this.time = time;
        this.appVersion = appVersion;
        this.desktop = desktop;
        var today = time.GetLocalNow().Date;
        periodFrom = today.AddDays(-6);
        periodTo = today;
        changes.Changed += async (_, _) =>
        {
            if (loaded)
            {
                await ReloadHistoryAsync().ConfigureAwait(true);
            }
        };
    }

    public ObservableCollection<ReportRunChoice> Runs { get; } = [];

    public ObservableCollection<string> Targets { get; } = [];

    public ObservableCollection<string> Findings { get; } = [];

    public bool HasPreview => PingCount > 0;

    public bool HasSaved => SavedPath is not null;

    public bool GraphShowsOtherData => sources.GraphShowsOtherData;

    public string GraphLabel => sources.GraphShowsOtherData ? sources.GraphLabel : "Open a run or import a file on the Graph page first.";

    public bool IsLiveSession
    {
        get => SourceKind == ReportSourceKind.LiveSession;
        set => Choose(value, ReportSourceKind.LiveSession);
    }

    public bool IsGraphView
    {
        get => SourceKind == ReportSourceKind.GraphView;
        set => Choose(value, ReportSourceKind.GraphView);
    }

    public bool IsStoredRun
    {
        get => SourceKind == ReportSourceKind.StoredRun;
        set => Choose(value, ReportSourceKind.StoredRun);
    }

    public bool IsHistoryPeriod
    {
        get => SourceKind == ReportSourceKind.HistoryPeriod;
        set => Choose(value, ReportSourceKind.HistoryPeriod);
    }

    /// <summary>Loads the history the first time the page opens, and picks a sensible source.</summary>
    public async Task EnsureLoadedAsync()
    {
        if (loaded)
        {
            OnPropertyChanged(nameof(GraphShowsOtherData));
            OnPropertyChanged(nameof(GraphLabel));
            await RefreshPreviewAsync().ConfigureAwait(true);
            return;
        }

        loaded = true;
        quiet = true;
        await ReloadHistoryAsync().ConfigureAwait(true);
        SourceKind = sources.LiveSession().Count > 0 || Runs.Count == 0 ? ReportSourceKind.LiveSession : ReportSourceKind.StoredRun;
        quiet = false;
        await RefreshPreviewAsync().ConfigureAwait(true);
    }

    /// <summary>For the History page's "Report" button: this run, ready to save.</summary>
    public async Task SelectRunAsync(long runId)
    {
        quiet = true;
        if (!loaded)
        {
            loaded = true;
            await ReloadHistoryAsync().ConfigureAwait(true);
        }

        SelectedRun = Runs.FirstOrDefault(run => run.Id == runId) ?? SelectedRun;
        SourceKind = ReportSourceKind.StoredRun;
        quiet = false;
        await RefreshPreviewAsync().ConfigureAwait(true);
    }

    /// <summary>For the Graph page's "Report" item: what the Graph shows, or the live session.</summary>
    public async Task SelectGraphAsync()
    {
        quiet = true;
        if (!loaded)
        {
            loaded = true;
            await ReloadHistoryAsync().ConfigureAwait(true);
        }

        OnPropertyChanged(nameof(GraphShowsOtherData));
        OnPropertyChanged(nameof(GraphLabel));
        SourceKind = sources.GraphShowsOtherData ? ReportSourceKind.GraphView : ReportSourceKind.LiveSession;
        quiet = false;
        await RefreshPreviewAsync().ConfigureAwait(true);
    }

    partial void OnSourceKindChanged(ReportSourceKind value)
    {
        OnPropertyChanged(nameof(IsLiveSession));
        OnPropertyChanged(nameof(IsGraphView));
        OnPropertyChanged(nameof(IsStoredRun));
        OnPropertyChanged(nameof(IsHistoryPeriod));
        PreviewSoon();
    }

    partial void OnSelectedRunChanged(ReportRunChoice? value)
    {
        if (value is not null && SourceKind != ReportSourceKind.StoredRun && !quiet)
        {
            SourceKind = ReportSourceKind.StoredRun;
            return;
        }

        PreviewSoon();
    }

    partial void OnSelectedTargetChanged(string? value) => PreviewSoon();

    partial void OnPeriodFromChanged(DateTime value) => PreviewSoon();

    partial void OnPeriodToChanged(DateTime value) => PreviewSoon();

    partial void OnIncludeSpeedTestsChanged(bool value) => PreviewSoon();

    [RelayCommand]
    private Task RefreshPreviewAsync() => PreviewAsync();

    private bool CanSave() => PingCount > 0 && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SavePdfAsync() => SaveAsync(ReportFormat.Pdf);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveExcelAsync() => SaveAsync(ReportFormat.Excel);

    [RelayCommand(CanExecute = nameof(HasSaved))]
    private void OpenSaved()
    {
        if (SavedPath is { } path)
        {
            desktop.Open(path);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSaved))]
    private void ShowSavedInFolder()
    {
        if (Path.GetDirectoryName(SavedPath) is { Length: > 0 } folder)
        {
            desktop.Open(folder);
        }
    }

    private void Choose(bool chosen, ReportSourceKind kind)
    {
        if (chosen)
        {
            SourceKind = kind;
        }
    }

    private async void PreviewSoon()
    {
        if (loaded && !quiet)
        {
            await PreviewAsync().ConfigureAwait(true);
        }
    }

    private async Task ReloadHistoryAsync()
    {
        try
        {
            storedRuns = await sources.ListRunsAsync(CancellationToken.None).ConfigureAwait(true);
            storedSpeedTests = await sources.SpeedTestsAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (HistoryException exception)
        {
            storedRuns = [];
            storedSpeedTests = [];
            PreviewMessage = $"The history could not be read: {exception.Message}";
        }

        var wasQuiet = quiet;
        quiet = true;
        var runId = SelectedRun?.Id;
        Runs.Clear();
        foreach (var run in storedRuns.Where(run => run.Summary.Sent > 0))
        {
            Runs.Add(new ReportRunChoice(run));
        }

        SelectedRun = Runs.FirstOrDefault(run => run.Id == runId) ?? Runs.FirstOrDefault();

        var target = SelectedTarget;
        Targets.Clear();
        foreach (var host in storedRuns.Select(run => run.TargetHost).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            Targets.Add(host);
        }

        SelectedTarget = Targets.FirstOrDefault(host => string.Equals(host, target, StringComparison.OrdinalIgnoreCase)) ?? Targets.FirstOrDefault();
        quiet = wasQuiet;
        PreviewSoon();
    }

    private async Task PreviewAsync()
    {
        var version = ++previewVersion;
        IReadOnlyList<PingAttempt> attempts;
        string about;
        try
        {
            (attempts, about, var empty) = await SelectionAsync().ConfigureAwait(true);
            if (version != previewVersion)
            {
                return;
            }

            if (attempts.Count == 0)
            {
                Clear(empty);
                return;
            }
        }
        catch (HistoryException exception)
        {
            Clear($"The history could not be read: {exception.Message}");
            return;
        }

        var input = Input(attempts, about, IncludeSpeedTests ? storedSpeedTests : [], connection: null, publicIp: null);
        var report = await Task.Run(() => ReportBuilder.Build(input)).ConfigureAwait(true);
        if (version != previewVersion)
        {
            return;
        }

        selected = attempts;
        subject = about;
        Stats = StatisticsDisplay.From(report.Statistics);
        PreviewHeading = $"{report.Target}  ·  {report.PeriodText}";
        PreviewMessage = string.Empty;
        Findings.Clear();
        foreach (var finding in report.Findings)
        {
            Findings.Add(finding);
        }

        PingCount = attempts.Count;
    }

    private async Task<(IReadOnlyList<PingAttempt> Attempts, string Subject, string Empty)> SelectionAsync()
    {
        switch (SourceKind)
        {
            case ReportSourceKind.GraphView when sources.GraphShowsOtherData:
                return (sources.GraphAttempts(), sources.GraphLabel, "The Graph has no pings to report on.");
            case ReportSourceKind.GraphView:
                return ([], string.Empty, "The Graph shows the live session. Open a run from the History or import a file on the Graph page to report on it.");
            case ReportSourceKind.StoredRun when SelectedRun is { } run:
                var runSubject = run.Run.Source == RunSource.Imported ? $"Imported from {run.Run.SourceName}" : "Stored ping run";
                return (await sources.LoadRunAsync(run.Id, CancellationToken.None).ConfigureAwait(true), runSubject, "This run has no pings.");
            case ReportSourceKind.StoredRun:
                return ([], string.Empty, "There are no stored runs yet. Every run on the Monitor page is kept in the History.");
            case ReportSourceKind.HistoryPeriod when SelectedTarget is { } target:
                var from = LocalMidnight(PeriodFrom <= PeriodTo ? PeriodFrom : PeriodTo);
                var to = LocalMidnight((PeriodFrom <= PeriodTo ? PeriodTo : PeriodFrom).AddDays(1));
                var attempts = await sources.LoadPeriodAsync(storedRuns, target, from, to, CancellationToken.None).ConfigureAwait(true);
                var days = from.ToString("d MMM", CultureInfo.CurrentCulture) + " – " + to.AddDays(-1).ToString("d MMM yyyy", CultureInfo.CurrentCulture);
                return (attempts, $"Stored pings, {days}", $"No stored pings to {target} between {days}.");
            case ReportSourceKind.HistoryPeriod:
                return ([], string.Empty, "There are no stored runs yet. Every run on the Monitor page is kept in the History.");
            default:
                return (sources.LiveSession(), "Live session", "The live session has no pings yet. Start a run on the Monitor page.");
        }
    }

    private async Task SaveAsync(ReportFormat format)
    {
        var writer = writers.First(candidate => candidate.Format == format);
        var attempts = selected;
        if (attempts.Count == 0
            || desktop.PickFileToSave(format == ReportFormat.Pdf ? "Save report as PDF" : "Save report as Excel", writer.FileFilter, SuggestedName(attempts, writer.FileExtension)) is not { } path)
        {
            return;
        }

        IsBusy = true;
        SavedText = "Making the report…";
        try
        {
            var speedTests = IncludeSpeedTests ? await sources.SpeedTestsAsync(CancellationToken.None).ConfigureAwait(true) : [];
            var connection = IncludeConnection ? sources.Connection() : null;
            var publicIp = IncludePublicIp ? await sources.PublicIpAsync(CancellationToken.None).ConfigureAwait(true) : null;
            var input = Input(attempts, subject, speedTests, connection, publicIp);
            var report = await Task.Run(() => ReportBuilder.Build(input)).ConfigureAwait(true);
            report = report with { Charts = charts.Draw(report) };

            using var buffer = new MemoryStream();
            await writer.WriteAsync(report, buffer, CancellationToken.None).ConfigureAwait(true);
            await File.WriteAllBytesAsync(path, buffer.ToArray()).ConfigureAwait(true);

            SavedPath = path;
            SavedText = $"Saved {Path.GetFileName(path)} ({Units.Bytes(buffer.Length)}) with {Units.CountOf(attempts.Count, "ping", "pings")}.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or HistoryException or InvalidOperationException)
        {
            SavedText = string.Empty;
            desktop.ShowError("The report could not be saved", exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private ReportInput Input(
        IReadOnlyList<PingAttempt> attempts,
        string about,
        IReadOnlyList<SpeedTestResult> speedTests,
        Core.Network.ConnectionSnapshot? connection,
        string? publicIp)
    {
        var accent = AccentPalette.For(settings.Current.Accent).OnLight;
        var options = new ReportOptions
        {
            Title = Title,
            Notes = Notes,
            IncludeSpeedTests = IncludeSpeedTests,
            IncludeConnection = IncludeConnection,
            IncludePublicIp = IncludePublicIp,
            IncludeAllPings = IncludeAllPings,
            AccentColor = $"#{accent.R:X2}{accent.G:X2}{accent.B:X2}",
        };
        return new ReportInput(about, attempts, speedTests, connection, publicIp, options, time.GetLocalNow(), appVersion);
    }

    private void Clear(string message)
    {
        selected = [];
        PingCount = 0;
        Stats = StatisticsDisplay.Empty;
        PreviewHeading = string.Empty;
        PreviewMessage = message;
        Findings.Clear();
    }

    private DateTimeOffset LocalMidnight(DateTime day)
    {
        var midnight = day.Date;
        return new DateTimeOffset(midnight, time.LocalTimeZone.GetUtcOffset(midnight));
    }

    private static string SuggestedName(IReadOnlyList<PingAttempt> attempts, string extension)
    {
        var hosts = attempts.Select(attempt => attempt.TargetHost).Distinct(StringComparer.OrdinalIgnoreCase).Take(2).ToList();
        var host = hosts.Count == 1 ? hosts[0] : "several-targets";
        var safe = string.Concat(host.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '-' : character));
        var start = attempts.Min(attempt => attempt.Timestamp);
        return $"{safe}-report-{start.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture)}{extension}";
    }
}
