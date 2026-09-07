namespace KanTab.Storage.Supabase;

public enum SyncStatus
{
    LocalOnly,
    Connecting,
    Synced,
    Syncing,
    Offline,
    SyncIssue,
    ConflictNeedsAttention
}
