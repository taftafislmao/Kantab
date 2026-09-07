using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace KanTab.Converters;

public class SyncStatusToBrushConverter : IValueConverter
{
    public static SyncStatusToBrushConverter Instance { get; } = new();

    private static readonly SolidColorBrush SyncedBrush = new(Color.Parse("#4CAF50"));
    private static readonly SolidColorBrush SyncingBrush = new(Color.Parse("#2196F3"));
    private static readonly SolidColorBrush LocalOnlyBrush = new(Color.Parse("#9E9E9E"));
    private static readonly SolidColorBrush OfflineBrush = new(Color.Parse("#FF9800"));
    private static readonly SolidColorBrush ErrorBrush = new(Color.Parse("#F44336"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (value as string) switch
        {
            "Synced" => SyncedBrush,
            "Syncing" => SyncingBrush,
            "Local only" => LocalOnlyBrush,
            "Offline" => OfflineBrush,
            "Sync issue" => ErrorBrush,
            _ => LocalOnlyBrush
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
