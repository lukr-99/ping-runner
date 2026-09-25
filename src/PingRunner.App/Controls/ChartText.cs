using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace PingRunner.App.Controls;

/// <summary>Text drawn by the charts in the element's inherited font, sharp at its own DPI.</summary>
internal static class ChartText
{
    public static FormattedText Create(Visual owner, DependencyObject fontSource, string text, double size, Brush brush, FontWeight weight)
    {
        var typeface = new Typeface(TextElement.GetFontFamily(fontSource), FontStyles.Normal, weight, FontStretches.Normal);
        return new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            size,
            brush,
            VisualTreeHelper.GetDpi(owner).PixelsPerDip);
    }

    /// <summary>
    /// A value axis from zero with about four round steps (1, 2, 2.5 or 5 times a power of ten) whose
    /// top is at or above <paramref name="maximum"/>.
    /// </summary>
    public static (double Top, double Step) Axis(double maximum, double floor)
    {
        var value = double.IsFinite(maximum) ? Math.Max(maximum, floor) : floor;
        var rough = value / 4;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rough)));
        var step = new[] { 1, 2, 2.5, 5, 10 }.Select(factor => factor * magnitude).First(candidate => candidate >= rough);
        return (Math.Ceiling(value / step) * step, step);
    }
}
