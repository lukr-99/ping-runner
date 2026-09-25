namespace PingRunner.Core.Reports;

/// <summary>A chart drawn for a report, as PNG bytes, with the size it was drawn at.</summary>
public sealed record ReportChart(string Title, string Caption, byte[] Png, int PixelWidth, int PixelHeight)
{
    public double AspectRatio => PixelHeight == 0 ? 0 : (double)PixelWidth / PixelHeight;
}
