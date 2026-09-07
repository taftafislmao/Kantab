using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KanTab.Models;
using KanTab.Storage;

namespace KanTab.Storage.Supabase;

/// <summary>
/// Dedicated synchronization service that manages realtime subscriptions,
/// uploads queued local changes, retries failed requests with exponential
/// backoff, and applies remote changes safely to local data.
/// </summary>
public class SyncService : ISyncService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly SupabaseConfig _config;
    private readonly ICloudKanTabRepository _cloudRepository;
    private readonly IOutboxRepository _outbox;
    private readonly string _deviceId;
    private readonly Func<string>? _accessTokenProvider;
    private readonly object _gate = new();
    private readonly TimeSpan[] _backoffIntervals = { TimeSpan.Zero, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30) };

    private CancellationTokenSource? _cancellationTokenSource;
    private string? _currentUserId;
    private bool _isDisposed;
    private SyncStatus _status = SyncStatus.LocalOnly;
    private bool _isProcessingOutbox;

    public SyncStatus Status
    {
        get => _status;
        private set
        {
            if (_status != value)
            {
                _status = value;
                StatusChanged?.Invoke(this, new SyncStatusChangedEventArgs { Status = value });
            }
        }
    }

    public event EventHandler<SyncStatusChangedEventArgs>? StatusChanged;
    public event EventHandler<RemoteChangeEventArgs>? RemoteChangeReceived;

    public SyncService(SupabaseConfig config, ICloudKanTabRepository cloudRepository, IOutboxRepository? outbox = null, Func<string>? accessTokenProvider = null)
    {
        _config = config;
        _cloudRepository = cloudRepository;
        _outbox = outbox ?? new LocalJsonOutboxRepository();
        _accessTokenProvider = accessTokenProvider;
        _deviceId = DeviceIdProvider.DeviceId;
    }

    public async Task StartAsync(string userId)
    {
        if (_isDisposed || !_config.IsConfigured)
            return;

        lock (_gate)
        {
            if (_currentUserId == userId && _cancellationTokenSource != null)
                return;

            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            _currentUserId = userId;
            Status = SyncStatus.Connecting;
        }

        try
        {
            await ProcessOutboxAsync().ConfigureAwait(false);

            var cloudData = await _cloudRepository.LoadAsync(userId).ConfigureAwait(false);
            if (cloudData != null)
            {
                Status = SyncStatus.Synced;
            }
        }
        catch (Exception ex)
        {
            Status = SyncStatus.SyncIssue;
            StatusChanged?.Invoke(this, new SyncStatusChangedEventArgs
            {
                Status = SyncStatus.SyncIssue,
                Message = ex.Message
            });
        }
    }

    public async Task StopAsync()
    {
        lock (_gate)
        {
            _cancellationTokenSource?.Cancel();
            _currentUserId = null;
            Status = SyncStatus.LocalOnly;
        }

        // Give the loop a chance to observe cancellation and reset processing flag.
        await Task.Delay(100).ConfigureAwait(false);
        lock (_gate) { _isProcessingOutbox = false; }
    }

    public async Task QueueBoardChangeAsync(Board board)
    {
        if (_isDisposed || _currentUserId == null)
            return;

        var payload = JsonSerializer.Serialize(board, JsonOptions);
        var entry = new OutboxEntry
        {
            EntityType = "board",
            EntityId = board.Id,
            OperationType = "update",
            Payload = payload,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _outbox.AddAsync(entry).ConfigureAwait(false);
        _ = Task.Run(() => ProcessOutboxAsync());
    }

    public async Task QueueColumnChangeAsync(KanBanColumn column, string boardId)
    {
        if (_isDisposed || _currentUserId == null)
            return;

        var payload = JsonSerializer.Serialize(new { column, boardId }, JsonOptions);
        var entry = new OutboxEntry
        {
            EntityType = "column",
            EntityId = column.Id,
            OperationType = "update",
            Payload = payload,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _outbox.AddAsync(entry).ConfigureAwait(false);
        _ = Task.Run(() => ProcessOutboxAsync());
    }

    public async Task QueueTaskChangeAsync(TaskItem task)
    {
        if (_isDisposed || _currentUserId == null)
            return;

        var payload = JsonSerializer.Serialize(task, JsonOptions);
        var entry = new OutboxEntry
        {
            EntityType = "task",
            EntityId = task.Id,
            OperationType = "update",
            Payload = payload,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _outbox.AddAsync(entry).ConfigureAwait(false);
        _ = Task.Run(() => ProcessOutboxAsync());
    }

    public async Task QueueNoteChangeAsync(Note note)
    {
        if (_isDisposed || _currentUserId == null)
            return;

        var entries = await _outbox.GetAllPendingAsync().ConfigureAwait(false);
        var existingNoteEntries = entries
            .Where(e => e.EntityType == "note" && e.EntityId == note.Id && e.OperationType != "soft_delete")
            .OrderByDescending(e => e.CreatedAt)
            .ToList();

        foreach (var existing in existingNoteEntries)
        {
            await _outbox.RemoveAsync(existing.OperationId).ConfigureAwait(false);
        }

        var payload = JsonSerializer.Serialize(note, JsonOptions);
        var entry = new OutboxEntry
        {
            EntityType = "note",
            EntityId = note.Id,
            OperationType = "update",
            Payload = payload,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _outbox.AddAsync(entry).ConfigureAwait(false);
        _ = Task.Run(() => ProcessOutboxAsync());
    }

    public async Task QueueSoftDeleteAsync(string entityType, string entityId, string userId)
    {
        if (_isDisposed || _currentUserId == null)
            return;

        var payload = JsonSerializer.Serialize(new { userId, entityType, entityId, deletedAt = DateTime.UtcNow }, JsonOptions);
        var entry = new OutboxEntry
        {
            EntityType = entityType,
            EntityId = entityId,
            OperationType = "soft_delete",
            Payload = payload,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _outbox.AddAsync(entry).ConfigureAwait(false);
        _ = Task.Run(() => ProcessOutboxAsync());
    }

    private async Task ProcessOutboxAsync()
    {
        if (_isProcessingOutbox)
            return;

        lock (_gate)
        {
            if (_isProcessingOutbox || _currentUserId == null)
                return;
            _isProcessingOutbox = true;
            Status = SyncStatus.Syncing;
        }

        try
        {
            var entries = await _outbox.GetAllPendingAsync().ConfigureAwait(false);
            if (entries.Count == 0)
            {
                Status = SyncStatus.Synced;
                return;
            }

            foreach (var entry in entries)
            {
                if (_cancellationTokenSource?.Token.IsCancellationRequested == true)
                    break;

                try
                {
                    await ProcessOutboxEntryAsync(entry).ConfigureAwait(false);
                    await _outbox.RemoveAsync(entry.OperationId).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    entry.RetryCount++;
                    entry.LastError = ex.Message;
                    entry.LastAttemptAt = DateTimeOffset.UtcNow;

                    if (entry.RetryCount >= _backoffIntervals.Length || IsAuthorizationError(ex))
                    {
                        Status = SyncStatus.SyncIssue;
                        StatusChanged?.Invoke(this, new SyncStatusChangedEventArgs
                        {
                            Status = SyncStatus.SyncIssue,
                            Message = $"Failed to sync: {ex.Message}"
                        });
                    }
                    else
                    {
                        var delay = _backoffIntervals[Math.Min(entry.RetryCount, _backoffIntervals.Length - 1)];
                        if (delay > TimeSpan.Zero)
                            await Task.Delay(delay, _cancellationTokenSource?.Token ?? CancellationToken.None).ConfigureAwait(false);
                    }

                    await _outbox.UpdateAsync(entry).ConfigureAwait(false);
                }
            }

            var remaining = await _outbox.GetAllPendingAsync().ConfigureAwait(false);
            if (remaining.Count == 0)
                Status = SyncStatus.Synced;
        }
        catch (Exception ex)
        {
            Status = SyncStatus.SyncIssue;
            StatusChanged?.Invoke(this, new SyncStatusChangedEventArgs
            {
                Status = SyncStatus.SyncIssue,
                Message = ex.Message
            });
        }
        finally
        {
            _isProcessingOutbox = false;
        }
    }

    private async Task ProcessOutboxEntryAsync(OutboxEntry entry)
    {
        if (_isDisposed || _currentUserId == null)
            return;

        switch (entry.OperationType)
        {
            case "insert":
            case "update":
                switch (entry.EntityType)
                {
                    case "board":
                        var boardDto = JsonSerializer.Deserialize<BoardDto>(entry.Payload ?? "{}", JsonOptions);
                        if (boardDto != null)
                        {
                            await UpsertBoardAsync(boardDto).ConfigureAwait(false);
                        }
                        break;
                    case "column":
                        var columnDto = JsonSerializer.Deserialize<ColumnDto>(entry.Payload ?? "{}", JsonOptions);
                        if (columnDto != null)
                        {
                            await UpsertColumnAsync(columnDto).ConfigureAwait(false);
                        }
                        break;
                    case "task":
                        var taskDto = JsonSerializer.Deserialize<TaskDto>(entry.Payload ?? "{}", JsonOptions);
                        if (taskDto != null)
                        {
                            await UpsertTaskAsync(taskDto).ConfigureAwait(false);
                        }
                        break;
                    case "note":
                        var noteDto = JsonSerializer.Deserialize<NoteDto>(entry.Payload ?? "{}", JsonOptions);
                        if (noteDto != null)
                        {
                            await UpsertNoteAsync(noteDto).ConfigureAwait(false);
                        }
                        break;
                }
                break;

            case "soft_delete":
                await SoftDeleteAsync(entry.EntityType, entry.EntityId).ConfigureAwait(false);
                break;
        }
    }

    private async Task UpsertBoardAsync(BoardDto dto)
    {
        var client = CreateHttpClient();
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{_config.Url}/rest/v1/boards", content).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private async Task UpsertColumnAsync(ColumnDto dto)
    {
        var client = CreateHttpClient();
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{_config.Url}/rest/v1/columns", content).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private async Task UpsertTaskAsync(TaskDto dto)
    {
        var client = CreateHttpClient();
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{_config.Url}/rest/v1/tasks", content).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private async Task UpsertNoteAsync(NoteDto dto)
    {
        var client = CreateHttpClient();
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{_config.Url}/rest/v1/notes", content).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private async Task SoftDeleteAsync(string entityType, string entityId)
    {
        var client = CreateHttpClient();
        var json = JsonSerializer.Serialize(new { deleted_at = DateTime.UtcNow }, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PatchAsync($"{_config.Url}/rest/v1/{entityType}?id=eq.{entityId}", content).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("apikey", _config.AnonKey);

        var token = _accessTokenProvider?.Invoke() ?? string.Empty;
        if (!string.IsNullOrEmpty(token))
        {
            client.DefaultRequestHeaders.Remove("Authorization");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        }

        return client;
    }

    private static bool IsAuthorizationError(Exception ex)
    {
        return ex is HttpRequestException ||
               (ex is System.Net.Http.HttpRequestException httpEx &&
                (httpEx.StatusCode == HttpStatusCode.Unauthorized ||
                 httpEx.StatusCode == HttpStatusCode.Forbidden));
    }

    public void Dispose()
    {
        _isDisposed = true;
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
    }

    public void OnRemoteChange(RealtimeEventArgs args)
    {
        if (_isDisposed || args.DeviceId == _deviceId)
            return;

        var remoteChange = new RemoteChangeEventArgs
        {
            EntityType = args.TableName,
            EntityId = "",
            Operation = args.Operation,
            Payload = args.Payload,
            DeviceId = args.DeviceId
        };

        RemoteChangeReceived?.Invoke(this, remoteChange);
    }
}
