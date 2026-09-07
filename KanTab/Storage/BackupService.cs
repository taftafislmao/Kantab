using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using KanTab.Models;
using KanTab.ViewModels;

namespace KanTab.Storage;

public static class BackupService
{
    public const int CurrentFormatVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Builds a backup document from the current workspace (no secrets).
    /// </summary>
    public static BackupDocument CreateBackup(WorkspaceState state)
    {
        return new BackupDocument
        {
            FormatVersion = CurrentFormatVersion,
            ExportedAt = DateTime.UtcNow,
            Data = new BackupPayload
            {
                Boards = state.Boards.OrderBy(b => b.Position).Select(CloneBoard).ToList(),
                Notes = state.Notes.OrderBy(n => n.Position).Select(CloneNote).ToList()
            }
        };
    }

    public static string Serialize(BackupDocument doc)
        => JsonSerializer.Serialize(doc, Options);

    /// <summary>
    /// Validates and deserializes the file contents. Does NOT mutate workspace.
    /// </summary>
    public static BackupImportResult TryParseAndValidate(string json, out KanTabData? sanitizedData, out string? error)
    {
        sanitizedData = null;
        error = null;
        BackupDocument? doc = null;
        try
        {
            doc = JsonSerializer.Deserialize<BackupDocument>(json, Options);
        }
        catch (Exception ex)
        {
            error = "Invalid backup file: " + ex.Message;
            return BackupImportResult.InvalidJson;
        }

        if (doc == null)
        {
            error = "Invalid backup file: empty document.";
            return BackupImportResult.InvalidJson;
        }

        if (doc.FormatVersion != CurrentFormatVersion)
        {
            error = $"Unsupported backup version {doc.FormatVersion}. Expected version {CurrentFormatVersion}.";
            return BackupImportResult.UnsupportedVersion;
        }

        if (doc.Data == null)
        {
            error = "Invalid backup: missing data.";
            return BackupImportResult.InvalidData;
        }

        // Secrets check: backup must not contain password/token/apiKey fields.
        // We do a cheap string scan for known secret-key substrings in the raw JSON
        // in case a future DTO accidentally includes them.
        var lower = json.ToLowerInvariant();
        foreach (var forbidden in new[] { "\"password\"", "\"token\"", "\"apikey\"", "\"api_key\"", "\"secret\"" })
        {
            if (lower.Contains(forbidden))
            {
                // Only reject if the serialized DTO actually contributed such a key.
                // Our BackupDocument never has these, so this would be a crafted file.
                // We reject to avoid importing secrets into workspace.
                error = "Backup contains unsupported fields.";
                return BackupImportResult.InvalidData;
            }
        }

        // Build a KanTabData and sanitize using existing rules (ids, ordering, timestamps).
        var candidate = new KanTabData
        {
            Boards = doc.Data.Boards ?? new List<Board>(),
            Notes = doc.Data.Notes ?? new List<Note>(),
            Settings = new AppSettings()
        };

        sanitizedData = LocalJsonKanTabRepository.Sanitize(candidate);
        return BackupImportResult.Ok;
    }

    /// <summary>
    /// Replaces the workspace with validated data and persists atomically.
    /// Caller must have already called TryParseAndValidate and gotten Ok + sanitizedData.
    /// </summary>
    public static bool ApplyImport(WorkspaceState state, KanTabData sanitizedData)
    {
        // Replace in-memory collections
        state.Boards.Clear();
        foreach (var b in sanitizedData.Boards.OrderBy(b => b.Position))
            state.Boards.Add(b);
        state.Notes.Clear();
        foreach (var n in sanitizedData.Notes.OrderBy(n => n.Position))
            state.Notes.Add(n);

        state.SelectedBoard = state.Boards.FirstOrDefault();
        state.SelectedNote = null;

        // Import is local-only: clear stale outbox ops that would overwrite imported state.
        try
        {
            var outboxPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KanTab", "outbox.json");
            if (File.Exists(outboxPath)) File.Delete(outboxPath);
        }
        catch { }

        // Notify projections then persist atomically (temp file + move)
        state.NotifyChanged();
        return state.SaveNow();
    }

    public static string SuggestedFileName(DateTime? exportedAtUtc = null)
    {
        var d = exportedAtUtc ?? DateTime.Now;
        return $"KanTab-Backup-{d:yyyy-MM-dd}.json";
    }

    private static Board CloneBoard(Board s)
    {
        var b = new Board
        {
            Id = s.Id,
            Name = s.Name,
            Position = s.Position,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt,
            ArchivedAt = s.ArchivedAt,
            DeletedAt = s.DeletedAt
        };
        foreach (var col in s.Columns.OrderBy(c => c.Position))
        {
            var nc = new KanBanColumn
            {
                Id = col.Id,
                Title = col.Title,
                BoardId = b.Id,
                Position = col.Position,
                CreatedAt = col.CreatedAt,
                UpdatedAt = col.UpdatedAt
            };
            foreach (var t in col.Tasks.OrderBy(t => t.Position))
            {
                nc.Tasks.Add(new TaskItem
                {
                    Id = t.Id,
                    BoardId = b.Id,
                    ColumnId = nc.Id,
                    Title = t.Title,
                    Description = t.Description,
                    Priority = t.Priority,
                    DueDate = t.DueDate,
                    DueTime = t.DueTime,
                    IsCompleted = t.IsCompleted,
                    Tags = new List<string>(t.Tags ?? new List<string>()),
                    Position = t.Position,
                    CreatedAt = t.CreatedAt,
                    UpdatedAt = t.UpdatedAt,
                    Checklist = new System.Collections.ObjectModel.ObservableCollection<ChecklistItem>(
                        t.Checklist.OrderBy(c => c.Position).Select(c => new ChecklistItem
                        {
                            Id = c.Id,
                            Text = c.Text,
                            IsCompleted = c.IsCompleted,
                            Position = c.Position,
                            CreatedAt = c.CreatedAt,
                            UpdatedAt = c.UpdatedAt
                        }))
                });
            }
            b.Columns.Add(nc);
        }
        return b;
    }

    private static Note CloneNote(Note s) => new()
    {
        Id = s.Id,
        Title = s.Title,
        Content = s.Content,
        IsPinned = s.IsPinned,
        Position = s.Position,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt
    };
}

public enum BackupImportResult
{
    Ok,
    InvalidJson,
    UnsupportedVersion,
    InvalidData
}
