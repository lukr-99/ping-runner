using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PingRunner.App.Converters;

/// <summary>Shows an "empty" hint while a count is zero.</summary>
public sealed class ZeroToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
