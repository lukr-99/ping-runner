using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PingRunner.Core.Pinging.Models;
using PingRunner.App.Features.Graphing.Models;
using PingRunner.App.Features.Graphing.Services;

namespace PingRunner.App.ViewModels;

public sealed class GraphWindowViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly MainWindowViewModel _mainWindowViewModel;
    private readonly ObservableCollection<PingAttemptResult> _importedAttempts = [];
    private IEnumerable<PingAttemptResult> _chartItemsSource;
    private GraphOption<GraphRangeMode> _selectedRangeMode;
    private GraphOption<int?> _selectedAttemptRange;
    private GraphOption<TimeSpan?> _selectedTimeWindow;
    private double _zoomFactor = 1;
    private double _panRatio;
    private bool _usingImportedData;
    private string _sourceLabel = "Live session";

    public GraphWindowViewModel(MainWindowViewModel mainWindowViewModel)
    {
        _mainWindowViewModel = mainWindowViewModel ?? throw new ArgumentNullException(nameof(mainWindowViewModel));

        RangeModes =
        [
            new("Recent Attempts", GraphRangeMode.RecentAttempts),
            new("Rolling Time", GraphRangeMode.RollingTimeWindow),
        ];
        AttemptRangeOptions =
        [
            new("Last 60", 60),
            new("Last 120", 120),
            new("Last 300", 300),
            new("All Stored", null),
        ];
        TimeWindowOptions =
        [
            new("1 minute", TimeSpan.FromMinutes(1)),
            new("5 minutes", TimeSpan.FromMinutes(5)),
            new("15 minutes", TimeSpan.FromMinutes(15)),
            new("60 minutes", TimeSpan.FromMinutes(60)),
            new("All Stored", null),
        ];

        _selectedRangeMode = RangeModes[0];
        _selectedAttemptRange = AttemptRangeOptions[1];
        _selectedTimeWindow = TimeWindowOptions[1];
        _chartItemsSource = _mainWindowViewModel.SessionAttempts;

        _mainWindowViewModel.SessionAttempts.CollectionChanged += OnSourceCollectionChanged;
        _mainWindowViewModel.PropertyChanged += OnMainWindowViewModelPropertyChanged;
        _importedAttempts.CollectionChanged += OnSourceCollectionChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<GraphOption<GraphRangeMode>> RangeModes { get; }

    public IReadOnlyList<GraphOption<int?>> AttemptRangeOptions { get; }

    public IReadOnlyList<GraphOption<TimeSpan?>> TimeWindowOptions { get; }

    public IEnumerable<PingAttemptResult> ChartItemsSource
    {
        get => _chartItemsSource;
        private set => SetField(ref _chartItemsSource, value);
    }

    public GraphOption<GraphRangeMode> SelectedRangeMode
    {
        get => _selectedRangeMode;
        set
        {
            if (!SetField(ref _selectedRangeMode, value))
            {
                return;
            }

            RefreshDerivedState();
        }
    }

    public GraphOption<int?> SelectedAttemptRange
    {
        get => _selectedAttemptRange;
        set
        {
            if (!SetField(ref _selectedAttemptRange, value))
            {
                return;
            }

            RefreshDerivedState();
        }
    }

    public GraphOption<TimeSpan?> SelectedTimeWindow
    {
        get => _selectedTimeWindow;
        set
        {
            if (!SetField(ref _selectedTimeWindow, value))
            {
                return;
            }

            RefreshDerivedState();
        }
    }

    public double ZoomFactor
    {
        get => _zoomFactor;
        set
        {
            if (!SetField(ref _zoomFactor, Math.Clamp(value, 1, 12)))
            {
                return;
            }

            if (_zoomFactor == 1 && PanRatio != 0)
            {
                PanRatio = 0;
            }

            RefreshDerivedState();
        }
    }

    public double PanRatio
    {
        get => _panRatio;
        set
        {
            if (!SetField(ref _panRatio, Math.Clamp(value, 0, 1)))
            {
                return;
            }

            RefreshDerivedState();
        }
    }

    public bool UsingImportedData
    {
        get => _usingImportedData;
        private set
        {
            if (!SetField(ref _usingImportedData, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CanUseLiveData));
        }
    }

    public bool CanUseLiveData => UsingImportedData;

    public bool IsAttemptRangeEnabled => SelectedRangeMode.Value == GraphRangeMode.RecentAttempts;

    public bool IsTimeWindowEnabled => SelectedRangeMode.Value == GraphRangeMode.RollingTimeWindow;

    public bool CanPan => ZoomFactor > 1;

    public string TargetHost => ResolveTargetHost(GetCurrentSourceAttempts());

    public string StatusText => UsingImportedData
        ? $"Imported data from {_sourceLabel}"
        : _mainWindowViewModel.StatusText;

    public int SentCount => GetCurrentSourceAttempts().Count;

    public string SuccessFailureSummary
    {
        get
        {
            var attempts = GetCurrentSourceAttempts();
            var successCount = attempts.Count(attempt => attempt.IsSuccess);
            var failureCount = attempts.Count - successCount;
            return $"{successCount} / {failureCount}";
        }
    }

    public string AverageLatencyText
    {
        get
        {
            var successfulLatencies = GetCurrentSourceAttempts()
                .Where(attempt => attempt.RoundtripTimeMilliseconds is not null)
                .Select(attempt => attempt.RoundtripTimeMilliseconds!.Value)
                .ToList();

            return successfulLatencies.Count == 0
                ? "-"
                : $"{Math.Round(successfulLatencies.Average(), 1):0.0} ms";
        }
    }

    public string SuccessRateText
    {
        get
        {
            var attempts = GetCurrentSourceAttempts();

            if (attempts.Count == 0)
            {
                return "-";
            }

            var successCount = attempts.Count(attempt => attempt.IsSuccess);
            return $"{(double)successCount / attempts.Count:P1}";
        }
    }

    public string PeakLatencyText
    {
        get
        {
            var successfulLatencies = GetSuccessfulLatenciesDescending();
            return successfulLatencies.Count == 0
                ? "-"
                : $"{successfulLatencies[0]} ms";
        }
    }

    public string Top50SpikeAverageText
    {
        get
        {
            var successfulLatencies = GetSuccessfulLatenciesDescending();

            if (successfulLatencies.Count == 0)
            {
                return "-";
            }

            var sampleCount = Math.Min(50, successfulLatencies.Count);
            return $"{Math.Round(successfulLatencies.Take(sampleCount).Average(), 1):0.0} ms";
        }
    }

    public string SourceSummaryText =>
        UsingImportedData
            ? $"Imported source: {_sourceLabel} ({SentCount} attempts)"
            : $"Live source: current session buffer ({SentCount} attempts, capped at 20,000)";

    public string ViewSummaryText
    {
        get
        {
            var viewportAttempts = GetCurrentViewAttempts();
            return viewportAttempts.Count == 0
                ? "No attempts match the current filter."
                : $"Current view contains {viewportAttempts.Count} attempts after range and zoom filtering.";
        }
    }

    public void LoadImportedAttempts(IEnumerable<PingAttemptResult> importedAttempts, string sourceLabel)
    {
        ArgumentNullException.ThrowIfNull(importedAttempts);

        _importedAttempts.Clear();

        foreach (var attempt in importedAttempts.OrderBy(attempt => attempt.Timestamp))
        {
            _importedAttempts.Add(attempt);
        }

        _sourceLabel = string.IsNullOrWhiteSpace(sourceLabel) ? "Imported session" : sourceLabel;
        UsingImportedData = true;
        ChartItemsSource = _importedAttempts;
        RefreshDerivedState();
    }

    public void UseLiveData()
    {
        UsingImportedData = false;
        _sourceLabel = "Live session";
        ChartItemsSource = _mainWindowViewModel.SessionAttempts;
        RefreshDerivedState();
    }

    public IReadOnlyList<PingAttemptResult> GetCurrentViewAttempts()
    {
        return GraphViewportService.BuildViewport(
            GetCurrentSourceAttempts(),
            SelectedRangeMode.Value,
            SelectedAttemptRange.Value,
            SelectedTimeWindow.Value,
            ZoomFactor,
            PanRatio);
    }

    public IReadOnlyList<PingAttemptResult> GetCurrentSourceAttemptsSnapshot()
    {
        return GetCurrentSourceAttempts();
    }

    public void Dispose()
    {
        _mainWindowViewModel.SessionAttempts.CollectionChanged -= OnSourceCollectionChanged;
        _mainWindowViewModel.PropertyChanged -= OnMainWindowViewModelPropertyChanged;
        _importedAttempts.CollectionChanged -= OnSourceCollectionChanged;
    }

    private IReadOnlyList<PingAttemptResult> GetCurrentSourceAttempts()
    {
        return ChartItemsSource
            .OrderBy(attempt => attempt.Timestamp)
            .ToList();
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshDerivedState();
    }

    private void OnMainWindowViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (UsingImportedData)
        {
            return;
        }

        if (e.PropertyName is nameof(MainWindowViewModel.StatusText) or nameof(MainWindowViewModel.TargetHost))
        {
            RefreshDerivedState();
        }
    }

    private void RefreshDerivedState()
    {
        OnPropertyChanged(nameof(TargetHost));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(SentCount));
        OnPropertyChanged(nameof(SuccessFailureSummary));
        OnPropertyChanged(nameof(AverageLatencyText));
        OnPropertyChanged(nameof(SuccessRateText));
        OnPropertyChanged(nameof(PeakLatencyText));
        OnPropertyChanged(nameof(Top50SpikeAverageText));
        OnPropertyChanged(nameof(SourceSummaryText));
        OnPropertyChanged(nameof(ViewSummaryText));
        OnPropertyChanged(nameof(IsAttemptRangeEnabled));
        OnPropertyChanged(nameof(IsTimeWindowEnabled));
        OnPropertyChanged(nameof(CanPan));
        OnPropertyChanged(nameof(ChartItemsSource));
    }

    private IReadOnlyList<long> GetSuccessfulLatenciesDescending()
    {
        return GetCurrentSourceAttempts()
            .Where(attempt => attempt.RoundtripTimeMilliseconds is not null)
            .Select(attempt => attempt.RoundtripTimeMilliseconds!.Value)
            .OrderByDescending(latency => latency)
            .ToList();
    }

    private static string ResolveTargetHost(IReadOnlyList<PingAttemptResult> attempts)
    {
        if (attempts.Count == 0)
        {
            return "No data loaded";
        }

        var distinctTargets = attempts
            .Select(attempt => attempt.TargetHost)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToList();

        return distinctTargets.Count == 1
            ? distinctTargets[0]
            : "Multiple targets";
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
