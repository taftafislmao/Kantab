using System;
using KanTab.Models;

namespace KanTab.Storage.Supabase;

public class RemoteChangeEventArgs : EventArgs
{
    public string EntityType { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string Operation { get; init; } = string.Empty; // "INSERT", "UPDATE", "DELETE"
    public object? Payload { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string? DeviceId { get; init; }
}
