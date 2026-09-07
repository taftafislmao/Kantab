using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace KanTab.Converters;

public class DateOnlyToDateTimeConverter : IValueConverter
{
    public static readonly DateOnlyToDateTimeConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateOnly dateOnly)
            return new DateTimeOffset(new DateTime(dateOnly.Year, dateOnly.Month, dateOnly.Day));
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTimeOffset dateTimeOffset)
            return DateOnly.FromDateTime(dateTimeOffset.Date);
        if (value is DateTime dateTime)
            return DateOnly.FromDateTime(dateTime);
        return null;
    }
}
