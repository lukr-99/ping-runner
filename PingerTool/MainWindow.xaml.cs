using System.Windows;
using PingerTool.Core.Pinging.Services;
using PingerTool.ViewModels;

namespace PingerTool;

public partial class MainWindow : Window
{
    private readonly PingService _pingService = new();
    private readonly MainWindowViewModel _viewModel = new();
    private GraphWindow? _graphWindow;
    private CancellationTokenSource? _runCancellationTokenSource;
    private bool _stopRequestedByUser;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    private async void StartButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_runCancellationTokenSource is not null)
        {
            return;
        }

        try
        {
            var settings = _viewModel.BuildSettings();

            _stopRequestedByUser = false;
            _runCancellationTokenSource = new CancellationTokenSource();
            _viewModel.BeginRun(settings);

            await foreach (var result in _pingService.RunAsync(settings, _runCancellationTokenSource.Token))
            {
                _viewModel.RegisterAttempt(result);
            }

            var finalStatus = _stopRequestedByUser
                ? $"Stopped pinging {settings.TargetHost}."
                : settings.RunForever
                    ? $"Stopped pinging {settings.TargetHost}."
                    : $"Finished pinging {settings.TargetHost}.";

            _viewModel.FinishRun(finalStatus);
        }
        catch (InvalidOperationException exception)
        {
            MessageBox.Show(this, exception.Message, "Invalid ping settings", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _runCancellationTokenSource?.Dispose();
            _runCancellationTokenSource = null;
            _stopRequestedByUser = false;
        }
    }

    private void StopButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_runCancellationTokenSource is null)
        {
            return;
        }

        _stopRequestedByUser = true;
        _viewModel.MarkStopping();
        _runCancellationTokenSource.Cancel();
    }

    private void GraphButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_graphWindow is { IsLoaded: true })
        {
            _graphWindow.Activate();
            _graphWindow.Focus();
            return;
        }

        _graphWindow = new GraphWindow(_viewModel)
        {
            Owner = this,
        };
        _graphWindow.Closed += GraphWindow_OnClosed;
        _graphWindow.Show();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_graphWindow is not null)
        {
            _graphWindow.Closed -= GraphWindow_OnClosed;
            _graphWindow.Close();
            _graphWindow = null;
        }

        base.OnClosed(e);
    }

    private void GraphWindow_OnClosed(object? sender, EventArgs e)
    {
        if (_graphWindow is null)
        {
            return;
        }

        _graphWindow.Closed -= GraphWindow_OnClosed;
        _graphWindow = null;
    }
}
