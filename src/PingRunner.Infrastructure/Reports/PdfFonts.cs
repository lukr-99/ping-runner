using PdfSharp.Fonts;

namespace PingRunner.Infrastructure.Reports;

/// <summary>PDFsharp takes one font resolver for the whole process; this installs it once, before the first PDF.</summary>
internal static class PdfFonts
{
    public const string Family = "Segoe UI";

    private static readonly Lock Gate = new();
    private static bool installed;

    public static void EnsureInstalled()
    {
        lock (Gate)
        {
            if (installed)
            {
                return;
            }

            GlobalFontSettings.FontResolver ??= new WindowsFontResolver();
            installed = true;
        }
    }
}
