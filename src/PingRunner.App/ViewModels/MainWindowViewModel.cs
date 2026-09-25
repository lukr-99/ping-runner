using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PingRunner.Core.Pinging.Models;

namespace PingRunner.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private const int MaximumVisibleAttempts = 500;
    private const int MaximumStoredAttempts = 20_000;

    private string _targetHost = "8.8.8.8";
    private int _timeoutMilliseconds = 1000;
    private int _intervalMilliseconds = 1000;
    private string _durationText = "00:05:00";
    private bool _runForever = true;
    private bool _isRunning;
    private string _statusText = "Idle";
    private int _sentCount;
    private int _successCount;
    private int _failureCount;
    private long _totalRoundtripMilliseconds;
    private int _latencySampleCount;
    private string? _currentIpAddress;
    private string _currentIpStatus = "Loading...";
    private bool _isCurrentIpVisible;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<PingAttemptResult> PingAttempts { get; } = [];

    public ObservableCollection<PingAttemptResult> SessionAttempts { get; } = [];

    public string TargetHost
    {
        get => _targetHost;
        set => SetField(ref _targetHost, value);
    }

    public int TimeoutMilliseconds
    {
        get => _timeoutMilliseconds;
        set => SetField(ref _timeoutMilliseconds, value);
    }

    public int IntervalMilliseconds
    {
        get => _intervalMilliseconds;
        set => SetField(ref _intervalMilliseconds, value);
    }

    public string DurationText
    {
        get => _durationText;
        set => SetField(ref _durationText, value);
    }

    public bool RunForever
    {
        get => _runForever;
        set => SetField(ref _runForever, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (!SetField(ref _isRunning, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CanStart));
            OnPropertyChanged(nameof(CanStop));
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public int SentCount
    {
        get => _sentCount;
        private set => SetField(ref _sentCount, value);
    }

    public int SuccessCount
    {
        get => _successCount;
        private set => SetField(ref _successCount, value);
    }

    public int FailureCount
    {
        get => _failureCount;
        private set => SetField(ref _failureCount, value);
    }

    public string SuccessFailureSummary => $"{SuccessCount} / {FailureCount}";

    public string AverageLatencyText =>
        _latencySampleCount == 0
            ? "-"
            : $"{Math.Round((double)_totalRoundtripMilliseconds / _latencySampleCount, 1):0.0} ms";

    public string SuccessRateText =>
        SentCount == 0
            ? "-"
            : $"{(double)SuccessCount / SentCount:P1}";

    public string CurrentIpDisplay
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_currentIpAddress))
            {
                return _currentIpStatus;
            }

            return _isCurrentIpVisible
                ? _currentIpAddress
                : "Hidden";
        }
    }

    public string CurrentIpToggleText => _isCurrentIpVisible ? "Hide" : "Show";

    public bool CanToggleCurrentIp => !string.IsNullOrWhiteSpace(_currentIpAddress);

    public bool CanStart => !IsRunning;

    public bool CanStop => IsRunning;

    public PingRunSettings BuildSettings()
    {
        var targetHost = TargetHost.Trim();

        if (string.IsNullOrWhiteSpace(targetHost))
        {
            throw new InvalidOperationException("Enter a host name or IP address.");
        }

        if (TimeoutMilliseconds <= 0)
        {
            throw new InvalidOperationException("Timeout must be greater than zero milliseconds.");
        }

        if (IntervalMilliseconds <= 0)
        {
            throw new InvalidOperationException("Interval must be greater than zero milliseconds.");
        }

        if (RunForever)
        {
            return new PingRunSettings(targetHost, TimeoutMilliseconds, IntervalMilliseconds, null, true);
        }

        if (!TimeSpan.TryParse(DurationText, out var duration) || duration <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Duration must be a positive time span such as 00:30:00.");
        }

        return new PingRunSettings(targetHost, TimeoutMilliseconds, IntervalMilliseconds, duration, false);
    }

    public void BeginRun(PingRunSettings settings)
    {
        PingAttempts.Clear();
        SessionAttempts.Clear();
        ResetStatistics();
        IsRunning = true;
        StatusText = settings.RunForever
            ? $"Pinging {settings.TargetHost} until stopped."
            : $"Pinging {settings.TargetHost} for {settings.RunDuration:hh\\:mm\\:ss}.";
    }

    public void MarkStopping()
    {
        StatusText = "Stopping...";
    }

    public void RegisterAttempt(PingAttemptResult attempt)
    {
        PingAttempts.Insert(0, attempt);
        SessionAttempts.Add(attempt);

        while (PingAttempts.Count > MaximumVisibleAttempts)
        {
            PingAttempts.RemoveAt(PingAttempts.Count - 1);
        }

        while (SessionAttempts.Count > MaximumStoredAttempts)
        {
            SessionAttempts.RemoveAt(0);
        }

        SentCount++;

        if (attempt.IsSuccess)
        {
            SuccessCount++;
        }
        else
        {
            FailureCount++;
        }

        if (attempt.RoundtripTimeMilliseconds is { } roundtripTimeMilliseconds)
        {
            _totalRoundtripMilliseconds += roundtripTimeMilliseconds;
            _latencySampleCount++;
        }

        OnPropertyChanged(nameof(SuccessFailureSummary));
        OnPropertyChanged(nameof(AverageLatencyText));
        OnPropertyChanged(nameof(SuccessRateText));
    }

    public void FinishRun(string statusText)
    {
        IsRunning = false;
        StatusText = statusText;
    }

    public void SetCurrentIpAddress(string ipAddress)
    {
        _currentIpAddress = ipAddress;
        _currentIpStatus = "Hidden";
        _isCurrentIpVisible = false;
        NotifyCurrentIpStateChanged();
    }

    public void SetCurrentIpUnavailable()
    {
        _currentIpAddress = null;
        _currentIpStatus = "Unavailable";
        _isCurrentIpVisible = false;
        NotifyCurrentIpStateChanged();
    }

    public void ToggleCurrentIpVisibility()
    {
        if (!CanToggleCurrentIp)
        {
            return;
        }

        _isCurrentIpVisible = !_isCurrentIpVisible;
        NotifyCurrentIpStateChanged();
    }

    private void ResetStatistics()
    {
        SentCount = 0;
        SuccessCount = 0;
        FailureCount = 0;
        _totalRoundtripMilliseconds = 0;
        _latencySampleCount = 0;

        OnPropertyChanged(nameof(SuccessFailureSummary));
        OnPropertyChanged(nameof(AverageLatencyText));
        OnPropertyChanged(nameof(SuccessRateText));
    }

    private void NotifyCurrentIpStateChanged()
    {
        OnPropertyChanged(nameof(CurrentIpDisplay));
        OnPropertyChanged(nameof(CurrentIpToggleText));
        OnPropertyChanged(nameof(CanToggleCurrentIp));
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
