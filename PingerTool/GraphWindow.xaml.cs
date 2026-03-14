using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using PingerTool.Features.Data.Services;
using PingerTool.ViewModels;

namespace PingerTool;

public partial class GraphWindow : Window
{
    private readonly PingAttemptCsvSerializer _csvSerializer = new();
    private readonly GraphWindowViewModel _viewModel;

    public GraphWindow(MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        _viewModel = new GraphWindowViewModel(viewModel);

        InitializeComponent();
        DataContext = _viewModel;
        Loaded += GraphWindow_OnLoaded;
    }

    private async void ImportDataButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import Ping Data",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            await using var stream = File.OpenRead(dialog.FileName);
            var importedAttempts = await _csvSerializer.ImportAsync(stream);
            _viewModel.LoadImportedAttempts(importedAttempts, Path.GetFileName(dialog.FileName));
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Import failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ExportVisibleDataButton_OnClick(object sender, RoutedEventArgs e)
    {
        await ExportAttemptsAsync(_viewModel.GetCurrentViewAttempts(), "visible");
    }

    private async void ExportSourceDataButton_OnClick(object sender, RoutedEventArgs e)
    {
        await ExportAttemptsAsync(_viewModel.GetCurrentSourceAttemptsSnapshot(), "source");
    }

    private void ExportPngButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export Graph View",
            Filter = "PNG image (*.png)|*.png",
            FileName = BuildSuggestedFileName("graph-view", "png"),
            OverwritePrompt = true,
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            ExportVisualToPng(ExportSurface, dialog.FileName);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "PNG export failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UseLiveDataButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.UseLiveData();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosed(e);
    }

    private void GraphWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        LatencyChart.InvalidateMeasure();
        LatencyChart.UpdateLayout();
        LatencyChart.InvalidateVisual();
    }

    private async Task ExportAttemptsAsync(IReadOnlyList<Core.Pinging.Models.PingAttemptResult> attempts, string exportSuffix)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export Ping Data",
            Filter = "CSV files (*.csv)|*.csv",
            FileName = BuildSuggestedFileName(exportSuffix, "csv"),
            OverwritePrompt = true,
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            await using var stream = File.Create(dialog.FileName);
            await _csvSerializer.ExportAsync(attempts, stream);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Export failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportVisualToPng(FrameworkElement visual, string filePath)
    {
        visual.UpdateLayout();

        if (visual.ActualWidth <= 0 || visual.ActualHeight <= 0)
        {
            throw new InvalidOperationException("The graph view has no visible size to export.");
        }

        var dpi = VisualTreeHelper.GetDpi(visual);
        var pixelWidth = Math.Max(1, (int)Math.Round(visual.ActualWidth * dpi.DpiScaleX));
        var pixelHeight = Math.Max(1, (int)Math.Round(visual.ActualHeight * dpi.DpiScaleY));

        var renderTargetBitmap = new RenderTargetBitmap(
            pixelWidth,
            pixelHeight,
            dpi.PixelsPerInchX,
            dpi.PixelsPerInchY,
            PixelFormats.Pbgra32);

        renderTargetBitmap.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(renderTargetBitmap));

        using var outputStream = File.Create(filePath);
        encoder.Save(outputStream);
    }

    private string BuildSuggestedFileName(string exportSuffix, string extension)
    {
        var rawTarget = string.IsNullOrWhiteSpace(_viewModel.TargetHost)
            ? "ping-session"
            : _viewModel.TargetHost;

        var invalidCharacters = Path.GetInvalidFileNameChars();
        var safeTarget = new string(rawTarget.Select(character => invalidCharacters.Contains(character) ? '-' : character).ToArray());
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        return $"{safeTarget}-{exportSuffix}-{timestamp}.{extension}";
    }
}
