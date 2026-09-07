using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace KanTab.Storage.Supabase;

public class LocalJsonOutboxRepository : IOutboxRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _filePath;

    public LocalJsonOutboxRepository(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KanTab", "outbox.json");
    }

    public async Task<List<OutboxEntry>> GetAllPendingAsync()
    {
        try
        {
            if (!File.Exists(_filePath))
                return new List<OutboxEntry>();

            var json = await File.ReadAllTextAsync(_filePath).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
                return new List<OutboxEntry>();

            var entries = JsonSerializer.Deserialize<List<OutboxEntry>>(json, JsonOptions);
            return entries ?? new List<OutboxEntry>();
        }
        catch
        {
            return new List<OutboxEntry>();
        }
    }

    public async Task AddAsync(OutboxEntry entry)
    {
        var entries = await GetAllPendingAsync().ConfigureAwait(false);
        entries.Add(entry);
        await SaveAsync(entries).ConfigureAwait(false);
    }

    public async Task UpdateAsync(OutboxEntry entry)
    {
        var entries = await GetAllPendingAsync().ConfigureAwait(false);
        var index = entries.FindIndex(e => e.OperationId == entry.OperationId);
        if (index >= 0)
            entries[index] = entry;
        else
            entries.Add(entry);

        await SaveAsync(entries).ConfigureAwait(false);
    }

    public async Task RemoveAsync(string operationId)
    {
        var entries = await GetAllPendingAsync().ConfigureAwait(false);
        entries.RemoveAll(e => e.OperationId == operationId);
        await SaveAsync(entries).ConfigureAwait(false);
    }

    public async Task<bool> HasPendingAsync()
    {
        var entries = await GetAllPendingAsync().ConfigureAwait(false);
        return entries.Count > 0;
    }

    private async Task SaveAsync(List<OutboxEntry> entries)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(entries, JsonOptions);
            await File.WriteAllTextAsync(_filePath, json).ConfigureAwait(false);
        }
        catch
        {
            // Outbox persistence failure must not break the app.
        }
    }
}
