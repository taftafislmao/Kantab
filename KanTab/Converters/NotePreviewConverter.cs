using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace KanTab.Converters;

public class NotePreviewConverter : IValueConverter
{
    public static NotePreviewConverter Instance { get; } = new();
    private const int MaxPreviewLength = 96;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string content)
            return string.Empty;

        var flattened = content.Replace("\n", " ").Replace("\r", " ").Trim();
        while (flattened.Contains("  "))
            flattened = flattened.Replace("  ", " ");

        if (flattened.Length <= MaxPreviewLength)
            return flattened;

        return flattened.Substring(0, MaxPreviewLength) + "…";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
