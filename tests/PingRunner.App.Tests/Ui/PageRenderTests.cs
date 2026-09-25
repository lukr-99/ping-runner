using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PingRunner.App.Shell;
using PingRunner.App.Tests.Hosting;
using PingRunner.Core.Settings;
using ThemeMode = PingRunner.Core.Settings.ThemeMode;

namespace PingRunner.App.Tests.Ui;

/// <summary>
/// Opens the real main window off screen with sample data, shows every page in light and dark, and
/// fails on any binding error. With PINGRUNNER_SCREENSHOTS set to a folder it also saves each page as
/// a PNG there (the README screenshots come from this).
/// </summary>
[Collection(WpfCollection.Name)]
public sealed class PageRenderTests
{
    private static readonly AppPage[] Pages = [AppPage.Monitor, AppPage.Graph, AppPage.SpeedTest, AppPage.Connection, AppPage.History, AppPage.Reports, AppPage.Settings];

    [Fact]
    public Task EveryPage_BothThemes_RendersWithoutBindingErrors() => WpfHost.RunAsync(async () =>
    {
        using var errors = new BindingErrorRecorder();
        using var app = TestApp.Create();
        await app.SeedHistoryAsync();
        await app.PingAsync(600);
        await app.RunSpeedTestAsync();
        await app.Graph.Connection.EnsureLoadedAsync();
        await app.Graph.History.EnsureLoadedAsync();
        await app.Graph.Reports.EnsureLoadedAsync();

        var window = new MainWindow(app.Graph)
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -30_000,
            Top = -30_000,
            Width = 1280,
            Height = 820,
            ShowActivated = false,
            ShowInTaskbar = false,
        };
        window.Show();
        try
        {
            var folder = Environment.GetEnvironmentVariable("PINGRUNNER_SCREENSHOTS");
            foreach (var mode in new[] { ThemeMode.Light, ThemeMode.Dark })
            {
                app.Graph.Theme.Apply(mode, AccentChoice.Teal);
                foreach (var page in Pages)
                {
                    window.Open(page);
                    await Settle();
                    var image = Render((FrameworkElement)window.Content);
                    Assert.True(image.PixelWidth > 0);
                    if (!string.IsNullOrEmpty(folder))
                    {
                        Save(image, Path.Combine(folder, $"{page.ToString().ToLowerInvariant()}-{mode.ToString().ToLowerInvariant()}.png"));
                    }
                }
            }
        }
        finally
        {
            window.Close();
        }

        Assert.Empty(errors.Errors);
    });

    // Page switches fade and slide in on the real clock; wait them out before capturing.
    private static async Task Settle()
    {
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        await Task.Delay(700);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    }

    private static RenderTargetBitmap Render(FrameworkElement element)
    {
        element.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)element.ActualWidth, (int)element.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        return bitmap;
    }

    private static void Save(BitmapSource image, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var output = File.Create(path);
        encoder.Save(output);
    }
}
