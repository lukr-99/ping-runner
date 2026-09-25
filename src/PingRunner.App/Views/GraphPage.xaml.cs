using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PingRunner.App.ViewModels;

namespace PingRunner.App.Views;

/// <summary>The Graph page. Exporting a PNG is a rendering job, so it lives here with the visuals.</summary>
public partial class GraphPage
{
    private readonly GraphViewModel viewModel;

    public GraphPage(GraphViewModel viewModel)
    {
        this.viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) => viewModel.Refresh();
    }

    /// <summary>Renders a framework element to a PNG at the screen's DPI.</summary>
    public static void RenderToPng(FrameworkElement element, string path)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.UpdateLayout();
        if (element.ActualWidth <= 0 || element.ActualHeight <= 0)
        {
            throw new InvalidOperationException("There is nothing on screen to export.");
        }

        var dpi = VisualTreeHelper.GetDpi(element);
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(element.ActualWidth * dpi.DpiScaleX),
            (int)Math.Ceiling(element.ActualHeight * dpi.DpiScaleY),
            dpi.PixelsPerInchX,
            dpi.PixelsPerInchY,
            PixelFormats.Pbgra32);
        bitmap.Render(element);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(path);
        encoder.Save(output);
    }

    private void ExportPng_OnClick(object sender, RoutedEventArgs e) =>
        viewModel.ExportImage(path => RenderToPng(ExportSurface, path));
}
