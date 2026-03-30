using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace OptiBat.Converters;

/// <summary>
/// Maps battery percentage to semantic color brush.
/// Green (>50%), Amber (20-50%), Red (<20%).
/// </summary>
public sealed class BatteryPercentToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not int percent) return Brushes.Gray;

        return percent switch
        {
            > 50 => new SolidColorBrush(Color.FromRgb(0x2E, 0x8B, 0x57)),  // Green
            > 20 => new SolidColorBrush(Color.FromRgb(0xCC, 0x7A, 0x00)),  // Amber
            _ => new SolidColorBrush(Color.FromRgb(0xCC, 0x33, 0x33)),      // Red
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts boolean to Visibility.
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var invert = parameter is string s && s == "Invert";
        var boolVal = value is bool b && b;
        if (invert) boolVal = !boolVal;
        return boolVal ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility v && v == Visibility.Visible;
}

/// <summary>
/// Converts watts to formatted string.
/// </summary>
public sealed class WattsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double watts) return "-- W";
        return watts < 0.1 ? "-- W" : $"{watts:F1} W";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Maps boolean IsActive to status indicator color.
/// </summary>
public sealed class ActiveStatusBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool active && active
            ? new SolidColorBrush(Color.FromRgb(0x2E, 0x8B, 0x57))  // Green
            : new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));  // Gray
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Inverts a boolean value.
/// </summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;
}
