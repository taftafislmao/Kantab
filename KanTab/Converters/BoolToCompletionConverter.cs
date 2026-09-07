using System;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace KanTab.Converters;

/// <summary>
/// Converts a boolean to a completion status string.
/// True returns "Completed", False returns "Active".
/// </summary>
public class BoolToCompletionConverter : IValueConverter
{
    public static BoolToCompletionConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? "Completed" : "Active";
        }
        return "Active";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
