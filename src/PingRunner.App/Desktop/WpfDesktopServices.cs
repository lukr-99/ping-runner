using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;

namespace PingRunner.App.Desktop;

/// <summary>Windows' file dialogs, message box, clipboard and shell, owned by the main window.</summary>
public sealed class WpfDesktopServices : IDesktopServices
{
    public string? PickFileToOpen(string title, string filter)
    {
        var dialog = new OpenFileDialog { Title = title, Filter = filter, CheckFileExists = true, Multiselect = false };
        return dialog.ShowDialog(Application.Current?.MainWindow) == true ? dialog.FileName : null;
    }

    public string? PickFileToSave(string title, string filter, string suggestedName)
    {
        var dialog = new SaveFileDialog { Title = title, Filter = filter, FileName = suggestedName, OverwritePrompt = true };
        return dialog.ShowDialog(Application.Current?.MainWindow) == true ? dialog.FileName : null;
    }

    public void ShowError(string title, string message)
    {
        var owner = Application.Current?.MainWindow;
        if (owner is null)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        else
        {
            MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public bool Confirm(string title, string message)
    {
        var owner = Application.Current?.MainWindow;
        var answer = owner is null
            ? MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
            : MessageBox.Show(owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        return answer == MessageBoxResult.Yes;
    }

    public void CopyText(string text) => Clipboard.SetText(text);

    public void Open(string target) => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose();
}
