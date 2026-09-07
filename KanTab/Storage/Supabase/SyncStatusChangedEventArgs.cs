using System;

namespace KanTab.Storage.Supabase;

public class SyncStatusChangedEventArgs : EventArgs
{
    public SyncStatus Status { get; init; }
    public string? Message { get; init; }
}
