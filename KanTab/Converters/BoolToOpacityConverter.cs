using System;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace KanTab.Converters;

/// <summary>
/// Converts a boolean to an opacity value.
/// True returns 1.0, False returns 0.5.
/// </summary>
public class BoolToOpacityConverter : IValueConverter
{
    public static BoolToOpacityConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? 1.0 : 0.5;
        }
        return 1.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
