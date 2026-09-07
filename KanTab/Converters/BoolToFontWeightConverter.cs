using System;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace KanTab.Converters;

/// <summary>
/// Converts a boolean to a FontWeight value.
/// True returns SemiBold, False returns Normal.
/// </summary>
public class BoolToFontWeightConverter : IValueConverter
{
    public static BoolToFontWeightConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? FontWeight.SemiBold : FontWeight.Normal;
        }
        return FontWeight.Normal;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
