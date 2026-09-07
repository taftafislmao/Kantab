using System;

namespace KanTab.Storage.Supabase;

public class OutboxEntry
{
    public string OperationId { get; set; } = Guid.NewGuid().ToString();
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty; // "insert", "update", "soft_delete"
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
}
