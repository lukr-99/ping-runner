namespace PingRunner.App.Desktop;

/// <summary>
/// The desktop's own dialogs and hand-offs, behind a seam so view models can be tested without
/// opening anything.
/// </summary>
public interface IDesktopServices
{
    /// <summary>The chosen file, or null when cancelled.</summary>
    string? PickFileToOpen(string title, string filter);

    /// <summary>The chosen file, or null when cancelled.</summary>
    string? PickFileToSave(string title, string filter, string suggestedName);

    void ShowError(string title, string message);

    /// <summary>Asks before something that cannot be undone; true to go ahead.</summary>
    bool Confirm(string title, string message);

    void CopyText(string text);

    /// <summary>Opens a web page or a folder with its default handler; only ever after a user click.</summary>
    void Open(string target);
}
