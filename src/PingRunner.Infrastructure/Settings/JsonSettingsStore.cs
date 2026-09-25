using System.Text.Json;
using System.Text.Json.Serialization;
using PingRunner.Core.Settings;

namespace PingRunner.Infrastructure.Settings;

/// <summary>
/// Settings as one JSON file. Saving writes a temporary file and moves it over the old one, so a crash
/// mid-write never leaves half a file. A file that cannot be read is kept beside it as
/// <c>settings.unreadable.json</c> and the defaults load, so a bad edit never stops the app starting.
/// </summary>
public sealed class JsonSettingsStore(string path) : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public AppSettings Load()
    {
        if (!File.Exists(path))
        {
            return AppSettings.Default;
        }

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Options);
            return (settings ?? AppSettings.Default).Normalized();
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            TryKeepUnreadable();
            return AppSettings.Default;
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings.Normalized(), Options));
        File.Move(temporary, path, overwrite: true);
    }

    private void TryKeepUnreadable()
    {
        try
        {
            var copy = Path.Combine(Path.GetDirectoryName(path) ?? ".", "settings.unreadable.json");
            File.Copy(path, copy, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Keeping the copy is a courtesy; the defaults load either way.
        }
    }
}
