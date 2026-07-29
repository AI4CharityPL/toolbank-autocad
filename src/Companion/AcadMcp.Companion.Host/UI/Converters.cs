using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AcadMcp.Companion.Host.UI;

/// <summary>Maps <c>false</c> to <see cref="Visibility.Visible"/> and <c>true</c> to Collapsed.</summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility v && v != Visibility.Visible;
}
