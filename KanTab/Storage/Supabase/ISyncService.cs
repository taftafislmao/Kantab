using System;
using System.Threading.Tasks;
using KanTab.Models;

namespace KanTab.Storage.Supabase;

public interface ISyncService : IDisposable
{
    SyncStatus Status { get; }

    Task StartAsync(string userId);
    Task StopAsync();

    Task QueueBoardChangeAsync(Board board);
    Task QueueColumnChangeAsync(KanBanColumn column, string boardId);
    Task QueueTaskChangeAsync(TaskItem task);
    Task QueueNoteChangeAsync(Note note);
    Task QueueSoftDeleteAsync(string entityType, string entityId, string userId);

    event EventHandler<SyncStatusChangedEventArgs>? StatusChanged;
    event EventHandler<RemoteChangeEventArgs>? RemoteChangeReceived;
}
