using System;
using System.Threading.Tasks;

namespace KanTab.Storage.Supabase;

/// <summary>
/// Interface for realtime database change subscriptions.
/// Implemented by WebSocket-based Supabase Realtime client.
/// </summary>
public interface IRealtimeSubscription : IDisposable
{
    event EventHandler<RealtimeEventArgs>? OnEvent;
    bool IsConnected { get; }
    Task SubscribeAsync(string tableName);
    Task UnsubscribeAsync();
}

public class RealtimeEventArgs : EventArgs
{
    public string TableName { get; init; } = string.Empty;
    public string Operation { get; init; } = string.Empty;
    public string? Payload { get; init; }
    public string? DeviceId { get; init; }
}
