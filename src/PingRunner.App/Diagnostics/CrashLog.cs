using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace PingRunner.App.Diagnostics;

/// <summary>
/// Writes an unhandled exception to <c>crash.log</c> in the data folder and tells the user where it
/// is, so a crash leaves something to report instead of a vanished window.
/// </summary>
public static class CrashLog
{
    public static void Install(Application application, string folder)
    {
        ArgumentNullException.ThrowIfNull(application);
        application.DispatcherUnhandledException += (_, e) => Report(folder, e);
    }

    private static void Report(string folder, DispatcherUnhandledExceptionEventArgs e)
    {
        var path = Path.Combine(folder, "crash.log");
        try
        {
            Directory.CreateDirectory(folder);
            File.AppendAllText(path, string.Create(CultureInfo.InvariantCulture, $"[{DateTimeOffset.Now:O}] {e.Exception}{Environment.NewLine}{Environment.NewLine}"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            path = "(could not be written)";
        }

        MessageBox.Show(
            $"Ping Runner hit an unexpected error and has to close.\n\n{e.Exception.Message}\n\nDetails: {path}",
            "Ping Runner",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
