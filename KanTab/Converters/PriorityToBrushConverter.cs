using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using KanTab.Models;

namespace KanTab.Converters;

public class PriorityToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush HighBrush = new(Color.Parse("#E5534B"));
    private static readonly SolidColorBrush MediumBrush = new(Color.Parse("#E5A04B"));
    private static readonly SolidColorBrush LowBrush = new(Color.Parse("#6A6A6A"));

    public static PriorityToBrushConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Priority priority)
        {
            return priority switch
            {
                Priority.High => HighBrush,
                Priority.Medium => MediumBrush,
                Priority.Low => LowBrush,
                _ => LowBrush
            };
        }
        return LowBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}