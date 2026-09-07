using System;
using System.Collections.Generic;
using System.Linq;
using KanTab.Models;
using KanTab.ViewModels;

namespace KanTab.Services;

public sealed record TaskSearchHit(TaskItem Task, string BoardName, string ColumnName);
public sealed record NoteSearchHit(Note Note);
public sealed record BoardSearchHit(Board Board);
public sealed record TagSearchHit(string Tag, int Count);

public sealed class GlobalSearchResult
{
    public IReadOnlyList<TaskSearchHit> TaskHits { get; init; } = Array.Empty<TaskSearchHit>();
    public IReadOnlyList<NoteSearchHit> NoteHits { get; init; } = Array.Empty<NoteSearchHit>();
    public IReadOnlyList<BoardSearchHit> BoardHits { get; init; } = Array.Empty<BoardSearchHit>();
    public IReadOnlyList<TagSearchHit> TagHits { get; init; } = Array.Empty<TagSearchHit>();

    public bool HasAnyResults => TaskHits.Count > 0 || NoteHits.Count > 0 || BoardHits.Count > 0 || TagHits.Count > 0;
    public int TotalCount => TaskHits.Count + NoteHits.Count + BoardHits.Count + TagHits.Count;
}

public static class GlobalSearchService
{
    public static GlobalSearchResult Search(WorkspaceState state, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new GlobalSearchResult();

        var trimmed = query.Trim();
        if (trimmed.Length == 0)
            return new GlobalSearchResult();

        var queryForTag = trimmed.TrimStart('#');

        var taskHits = new List<TaskSearchHit>();
        var noteHits = new List<NoteSearchHit>();
        var boardHits = new List<BoardSearchHit>();
        var tagMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var board in state.Boards)
        {
            if (!board.IsDeleted && board.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
            {
                boardHits.Add(new BoardSearchHit(board));
            }

            foreach (var column in board.Columns)
            {
                foreach (var task in column.Tasks)
                {
                    var matchesTitle = task.Title.Contains(trimmed, StringComparison.OrdinalIgnoreCase);
                    var matchesDesc = task.Description != null && task.Description.Contains(trimmed, StringComparison.OrdinalIgnoreCase);
                    var matchesTag = task.Tags.Any(t => t.Contains(trimmed, StringComparison.OrdinalIgnoreCase) || (!string.IsNullOrEmpty(queryForTag) && t.Contains(queryForTag, StringComparison.OrdinalIgnoreCase)));

                    if (matchesTitle || matchesDesc || matchesTag)
                    {
                        taskHits.Add(new TaskSearchHit(task, board.Name, column.Title));
                    }

                    foreach (var tag in task.Tags)
                    {
                        if (string.IsNullOrWhiteSpace(tag)) continue;
                        var key = tag.Trim();
                        if (!tagMap.ContainsKey(key))
                            tagMap[key] = 0;
                        tagMap[key]++;
                    }
                }
            }
        }

        foreach (var note in state.Notes)
        {
            if (note.Title.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                note.Content.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
            {
                noteHits.Add(new NoteSearchHit(note));
            }
        }

        var tagHits = new List<TagSearchHit>();
        if (!string.IsNullOrWhiteSpace(queryForTag))
        {
            tagHits = tagMap
                .Where(kv => kv.Key.Contains(queryForTag, StringComparison.OrdinalIgnoreCase))
                .Select(kv => new TagSearchHit(kv.Key, kv.Value))
                .OrderBy(t => t.Tag, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var orderedTasks = taskHits
            .OrderByDescending(h => h.Task.UpdatedAt)
            .ThenBy(h => h.Task.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var orderedNotes = noteHits
            .OrderByDescending(h => h.Note.IsPinned)
            .ThenByDescending(h => h.Note.UpdatedAt)
            .ThenBy(h => h.Note.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var orderedBoards = boardHits
            .OrderBy(h => h.Board.Position)
            .ThenBy(h => h.Board.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new GlobalSearchResult
        {
            TaskHits = orderedTasks,
            NoteHits = orderedNotes,
            BoardHits = orderedBoards,
            TagHits = tagHits
        };
    }
}
