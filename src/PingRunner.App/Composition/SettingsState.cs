using System.IO;
using PingRunner.Core.Settings;

namespace PingRunner.App.Composition;

/// <summary>
/// The settings in memory, saved on every change. A failed save (a full disk, a locked file) keeps
/// the change for this run and is reported once through <see cref="SaveFailed"/>.
/// </summary>
public sealed class SettingsState(ISettingsStore store)
{
    public event EventHandler? Changed;

    public event EventHandler<Exception>? SaveFailed;

    public AppSettings Current { get; private set; } = store.Load();

    public void Update(Func<AppSettings, AppSettings> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var updated = change(Current).Normalized();
        if (updated == Current)
        {
            return;
        }

        Current = updated;
        try
        {
            store.Save(updated);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SaveFailed?.Invoke(this, exception);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
