using PingRunner.App.Desktop;

namespace PingRunner.App.Tests.Fakes;

/// <summary>Answers dialogs with the paths the test set and records everything else.</summary>
public sealed class RecordingDesktopServices : IDesktopServices
{
    public string? FileToOpen { get; set; }

    public string? FileToSave { get; set; }

    public List<string> Copied { get; } = [];

    public List<string> Opened { get; } = [];

    public List<(string Title, string Message)> Errors { get; } = [];

    public string? PickFileToOpen(string title, string filter) => FileToOpen;

    public string? PickFileToSave(string title, string filter, string suggestedName) => FileToSave;

    public void ShowError(string title, string message) => Errors.Add((title, message));

    public void CopyText(string text) => Copied.Add(text);

    public void Open(string target) => Opened.Add(target);
}
