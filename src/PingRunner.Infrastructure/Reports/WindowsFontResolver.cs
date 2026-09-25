using PdfSharp.Fonts;

namespace PingRunner.Infrastructure.Reports;

/// <summary>
/// Gives PDFsharp the Segoe UI files from the Windows fonts folder for every family it asks for, so
/// reports look like the app. Arial stands in on a system without Segoe UI.
/// </summary>
internal sealed class WindowsFontResolver : IFontResolver
{
    private static readonly Dictionary<string, string> Fallbacks = new(StringComparer.OrdinalIgnoreCase)
    {
        ["segoeui.ttf"] = "arial.ttf",
        ["segoeuib.ttf"] = "arialbd.ttf",
        ["segoeuii.ttf"] = "ariali.ttf",
        ["segoeuiz.ttf"] = "arialbi.ttf",
    };

    private readonly string fontsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic) => new((bold, italic) switch
    {
        (true, true) => "segoeuiz.ttf",
        (true, false) => "segoeuib.ttf",
        (false, true) => "segoeuii.ttf",
        _ => "segoeui.ttf",
    });

    public byte[]? GetFont(string faceName)
    {
        foreach (var file in new[] { faceName, Fallbacks.GetValueOrDefault(faceName) })
        {
            var path = file is null ? null : Path.Combine(fontsFolder, file);
            if (path is not null && File.Exists(path))
            {
                return File.ReadAllBytes(path);
            }
        }

        return null;
    }
}
