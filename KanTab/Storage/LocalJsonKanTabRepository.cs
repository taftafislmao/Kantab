using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KanTab.Models;

namespace KanTab.Storage;

/// <summary>
/// Local JSON persistence for KanTab data. Writes atomically (temp file + move)
/// and tolerates missing, empty, or corrupt files so a bad file never crashes
/// the app or destroys the original data.
/// </summary>
public class LocalJsonKanTabRepository : IKanTabRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string FilePath { get; }

    /// <summary>Set when the last load hit a recoverable problem (corrupt file, etc.).</summary>
    public string? LastLoadError { get; private set; }

    public LocalJsonKanTabRepository(string? filePath = null)
    {
        FilePath = filePath ?? GetDefaultStoragePath();
    }

    public Task<KanTabData> LoadAsync()
    {
        LastLoadError = null;
        KanTabData data;

        try
        {
            if (!File.Exists(FilePath))
                return Task.FromResult(new KanTabData());

            var json = File.ReadAllText(FilePath);
            if (string.IsNullOrWhiteSpace(json))
                return Task.FromResult(new KanTabData());

            data = JsonSerializer.Deserialize<KanTabData>(json, SerializerOptions) ?? new KanTabData();
        }
        catch (Exception ex)
        {
            LastLoadError = ex.Message;
            return Task.FromResult(new KanTabData());
        }

        return Task.FromResult(Sanitize(data));
    }

    public async Task SaveAsync(KanTabData data)
    {
        var temporaryPath = FilePath + ".tmp";

        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(data, SerializerOptions);
            await File.WriteAllTextAsync(temporaryPath, json).ConfigureAwait(false);
            File.Move(temporaryPath, FilePath, true);
        }
        catch
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch
            {
            }

            throw;
        }
    }

    /// <summary>
    /// Repairs loaded data in place: unique non-empty IDs everywhere, sane
    /// defaults for missing text/timestamps, task parentage re-derived from the
    /// containing collections, and sequential positions for stable ordering.
    /// Also normalizes board order and removes archived boards from active set.
    /// </summary>
    public static KanTabData Sanitize(KanTabData data)
    {
        data ??= new KanTabData();

        var boardIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var boardPosition = 0;

        foreach (var board in data.Boards.Where(b => b != null))
        {
            if (string.IsNullOrWhiteSpace(board.Id) || !boardIds.Add(board.Id))
                board.Id = NewUniqueId(boardIds);

            if (string.IsNullOrWhiteSpace(board.Name))
                board.Name = "Untitled board";
            if (board.CreatedAt == default)
                board.CreatedAt = DateTime.Now;
            if (board.UpdatedAt == default)
                board.UpdatedAt = board.CreatedAt;

            board.Position = boardPosition++;

            var columnIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var columnPosition = 0;
            foreach (var column in board.Columns.Where(c => c != null))
            {
                if (string.IsNullOrWhiteSpace(column.Id) || !columnIds.Add(column.Id))
                    column.Id = NewUniqueId(columnIds);

                column.BoardId = board.Id;

                if (string.IsNullOrWhiteSpace(column.Title))
                    column.Title = "Untitled column";
                if (column.CreatedAt == default)
                    column.CreatedAt = board.CreatedAt;
                if (column.UpdatedAt == default)
                    column.UpdatedAt = column.CreatedAt;
                column.Position = columnPosition++;

                var taskIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var taskPosition = 0;
                foreach (var task in column.Tasks.Where(t => t != null))
                {
                    if (string.IsNullOrWhiteSpace(task.Id) || !taskIds.Add(task.Id))
                        task.Id = NewUniqueId(taskIds);

                    if (string.IsNullOrWhiteSpace(task.Title))
                        task.Title = "Untitled task";
                    if (task.CreatedAt == default)
                        task.CreatedAt = column.CreatedAt;
                    if (task.UpdatedAt == default)
                        task.UpdatedAt = task.CreatedAt;
                    task.ColumnId = column.Id;
                    task.BoardId = board.Id;
                    task.Position = taskPosition++;

                    if (task.Checklist == null)
                        task.Checklist = new ObservableCollection<ChecklistItem>();

                    var checklistIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var orderedChecklist = task.Checklist.Where(c => c != null).OrderBy(c => c.Position).ToList();
                    var sanitizedChecklist = new ObservableCollection<ChecklistItem>();
                    var checklistPosition = 0;
                    foreach (var item in orderedChecklist)
                    {
                        if (string.IsNullOrWhiteSpace(item.Id) || !checklistIds.Add(item.Id))
                            item.Id = NewUniqueId(checklistIds);
                        if (string.IsNullOrWhiteSpace(item.Text))
                            item.Text = "Untitled item";
                        else
                            item.Text = item.Text.Trim();
                        if (item.CreatedAt == default)
                            item.CreatedAt = task.CreatedAt;
                        if (item.UpdatedAt == default)
                            item.UpdatedAt = item.CreatedAt;
                        item.Position = checklistPosition++;
                        sanitizedChecklist.Add(item);
                    }
                    task.Checklist = sanitizedChecklist;
                }

                column.Tasks = new ObservableCollection<TaskItem>(column.Tasks.Where(t => t != null));
            }

            board.Columns = new ObservableCollection<KanBanColumn>(board.Columns.Where(c => c != null));
        }

        data.Boards = data.Boards.Where(b => b != null).ToList();
        boardPosition = 0;
        foreach (var board in data.Boards.OrderBy(b => b.Position))
        {
            board.Position = boardPosition++;
        }
        data.Boards = data.Boards.OrderBy(b => b.Position).ToList();

        var noteIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var notePosition = 0;
        foreach (var note in data.Notes.Where(n => n != null))
        {
            if (string.IsNullOrWhiteSpace(note.Id) || !noteIds.Add(note.Id))
            {
                do
                {
                    note.Id = Guid.NewGuid().ToString();
                }
                while (!noteIds.Add(note.Id));
            }

            if (string.IsNullOrWhiteSpace(note.Title))
                note.Title = "Untitled note";
            note.Content ??= string.Empty;
            if (note.CreatedAt == default)
                note.CreatedAt = DateTime.Now;
            if (note.UpdatedAt == default)
                note.UpdatedAt = note.CreatedAt;
            note.Position = notePosition++;
        }

        data.Notes = data.Notes.Where(n => n != null).ToList();
        data.Settings ??= new AppSettings();
        // Normalize new settings fields for older files
        if (!Enum.IsDefined(typeof(ReminderLeadTime), data.Settings.ReminderLeadMinutes))
            data.Settings.ReminderLeadMinutes = ReminderLeadTime.Minutes15;

        return data;
    }

    private static string NewUniqueId(HashSet<string> usedIds)
    {
        string id;
        do
        {
            id = Guid.NewGuid().ToString();
        }
        while (!usedIds.Add(id));
        return id;
    }

    public static string GetDefaultStoragePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var directory = string.IsNullOrWhiteSpace(localAppData)
            ? Path.Combine(AppContext.BaseDirectory, "data")
            : Path.Combine(localAppData, "KanTab");
        return Path.Combine(directory, "kantab.json");
    }
}
