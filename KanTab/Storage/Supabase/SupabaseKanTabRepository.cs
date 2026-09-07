using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using KanTab.Models;

namespace KanTab.Storage.Supabase;

/// <summary>
/// Supabase implementation of ICloudKanTabRepository. Maps between
/// desktop models and cloud DTOs. Keeps all Supabase-specific code
/// in this dedicated infrastructure class.
/// </summary>
public class SupabaseKanTabRepository : ICloudKanTabRepository, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly HttpClient _http;
    private readonly SupabaseConfig _config;
    private readonly string _baseUrl;

    public SupabaseKanTabRepository(SupabaseConfig config, HttpClient? httpClient = null)
    {
        _config = config;
        _baseUrl = config.Url.TrimEnd('/');
        _http = httpClient ?? new HttpClient();
        _http.DefaultRequestHeaders.Add("apikey", config.AnonKey);
    }

    public async Task<KanTabData> LoadAsync(string userId)
    {
        if (!_config.IsConfigured || string.IsNullOrWhiteSpace(userId))
            return new KanTabData();

        try
        {
            var boards = await FetchBoardsAsync(userId).ConfigureAwait(false);
            var notes = await FetchNotesAsync(userId).ConfigureAwait(false);
            return new KanTabData
            {
                Boards = boards,
                Notes = notes,
                Settings = new AppSettings()
            };
        }
        catch
        {
            return new KanTabData();
        }
    }

    public async Task SaveAsync(KanTabData data, string userId)
    {
        if (!_config.IsConfigured || string.IsNullOrWhiteSpace(userId))
            return;

        try
        {
            await SaveBoardsAsync(data.Boards, userId).ConfigureAwait(false);
            await SaveNotesAsync(data.Notes, userId).ConfigureAwait(false);
        }
        catch
        {
            throw;
        }
    }

    private async Task<List<Board>> FetchBoardsAsync(string userId)
    {
        var response = await _http.GetAsync($"{_baseUrl}/rest/v1/boards?user_id=eq.{userId}&order=position.asc").ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var dtos = JsonSerializer.Deserialize<List<BoardDto>>(json, JsonOptions) ?? new List<BoardDto>();
        return dtos.Select(dto => dto.ToBoard()).ToList();
    }

    private async Task SaveBoardsAsync(List<Board> boards, string userId)
    {
        foreach (var board in boards)
        {
            var dto = new BoardDto(board, userId);
            await UpsertBoardAsync(dto).ConfigureAwait(false);
            foreach (var column in board.Columns)
            {
                var colDto = new ColumnDto(column, board.Id, userId);
                await UpsertColumnAsync(colDto).ConfigureAwait(false);
                foreach (var task in column.Tasks)
                {
                    var taskDto = new TaskDto(task, board.Id, column.Id, userId);
                    await UpsertTaskAsync(taskDto).ConfigureAwait(false);
                }
            }
        }
    }

    private async Task<List<Note>> FetchNotesAsync(string userId)
    {
        var response = await _http.GetAsync($"{_baseUrl}/rest/v1/notes?user_id=eq.{userId}&order=position.asc").ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var dtos = JsonSerializer.Deserialize<List<NoteDto>>(json, JsonOptions) ?? new List<NoteDto>();
        return dtos.Select(dto => dto.ToNote()).ToList();
    }

    private async Task SaveNotesAsync(List<Note> notes, string userId)
    {
        foreach (var note in notes)
        {
            var dto = new NoteDto(note, userId);
            await UpsertNoteAsync(dto).ConfigureAwait(false);
        }
    }

    private async Task UpsertBoardAsync(BoardDto dto)
    {
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        await _http.PostAsync($"{_baseUrl}/rest/v1/boards", content).ConfigureAwait(false);
    }

    private async Task UpsertColumnAsync(ColumnDto dto)
    {
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        await _http.PostAsync($"{_baseUrl}/rest/v1/columns", content).ConfigureAwait(false);
    }

    private async Task UpsertTaskAsync(TaskDto dto)
    {
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        await _http.PostAsync($"{_baseUrl}/rest/v1/tasks", content).ConfigureAwait(false);
    }

    private async Task UpsertNoteAsync(NoteDto dto)
    {
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        await _http.PostAsync($"{_baseUrl}/rest/v1/notes", content).ConfigureAwait(false);
    }

    public void Dispose()
    {
    }
}

public class BoardDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Position { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ArchivedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public BoardDto() { }

    public BoardDto(Board board, string userId)
    {
        Id = board.Id;
        UserId = userId;
        Name = board.Name;
        Position = board.Position;
        CreatedAt = board.CreatedAt.ToUniversalTime();
        UpdatedAt = board.UpdatedAt.ToUniversalTime();
        ArchivedAt = board.ArchivedAt?.ToUniversalTime();
        DeletedAt = board.DeletedAt?.ToUniversalTime();
    }

    public Board ToBoard()
    {
        return new Board
        {
            Id = Id,
            Name = Name,
            Position = Position,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
            ArchivedAt = ArchivedAt,
            DeletedAt = DeletedAt,
            Columns = new ObservableCollection<KanBanColumn>()
        };
    }
}

public class ColumnDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string BoardId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Position { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    public ColumnDto() { }

    public ColumnDto(KanBanColumn column, string boardId, string userId)
    {
        Id = column.Id;
        UserId = userId;
        BoardId = boardId;
        Title = column.Title;
        Position = column.Position;
        CreatedAt = column.CreatedAt.ToUniversalTime();
        UpdatedAt = column.UpdatedAt.ToUniversalTime();
    }
}

public class TaskDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string BoardId { get; set; } = string.Empty;
    public string ColumnId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = "Medium";
    public DateOnly? DueDate { get; set; }
    public bool IsCompleted { get; set; }
    public int Position { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
    public List<ChecklistItem> Checklist { get; set; } = new();

    public TaskDto() { }

    public TaskDto(TaskItem task, string boardId, string columnId, string userId)
    {
        Id = task.Id;
        UserId = userId;
        BoardId = boardId;
        ColumnId = columnId;
        Title = task.Title;
        Description = task.Description;
        Priority = task.Priority.ToString();
        DueDate = task.DueDate;
        IsCompleted = task.IsCompleted;
        Position = task.Position;
        CreatedAt = task.CreatedAt.ToUniversalTime();
        UpdatedAt = task.UpdatedAt.ToUniversalTime();
        Checklist = task.Checklist.OrderBy(c => c.Position).Select(c => new ChecklistItem
        {
            Id = c.Id,
            Text = c.Text,
            IsCompleted = c.IsCompleted,
            Position = c.Position,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        }).ToList();
    }
}

public class NoteDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
    public int Position { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    public NoteDto() { }

    public NoteDto(Note note, string userId)
    {
        Id = note.Id;
        UserId = userId;
        Title = note.Title;
        Content = note.Content;
        IsPinned = note.IsPinned;
        Position = note.Position;
        CreatedAt = note.CreatedAt.ToUniversalTime();
        UpdatedAt = note.UpdatedAt.ToUniversalTime();
    }

    public Note ToNote()
    {
        return new Note
        {
            Id = Id,
            Title = Title,
            Content = Content,
            IsPinned = IsPinned,
            Position = Position,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt
        };
    }
}
