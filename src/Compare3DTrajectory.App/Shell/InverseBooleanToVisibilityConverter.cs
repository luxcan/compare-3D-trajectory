using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Compare3DTrajectory.App.Shell;

/// <summary>Visible when false: for the hints that stand in for something not there yet.</summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("The hint's visibility is never edited.");
}
