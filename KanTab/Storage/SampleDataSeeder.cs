using System;
using System.Collections.Generic;
using System.Linq;
using KanTab.Models;

namespace KanTab.Storage;

/// <summary>
/// Creates the sample content shown on a very first launch, before any
/// user data exists. Kept separate so it can later be removed or disabled
/// without touching user data.
/// </summary>
public static class SampleDataSeeder
{
    public static List<Board> CreateDefaultBoards()
    {
        var now = DateTime.Now;
        var board = new Board
        {
            Name = "My Board",
            Position = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        var todoColumn = new KanBanColumn { Title = "To Do", BoardId = board.Id, Position = 0, CreatedAt = now, UpdatedAt = now };
        var inProgressColumn = new KanBanColumn { Title = "In Progress", BoardId = board.Id, Position = 1, CreatedAt = now, UpdatedAt = now };
        var doneColumn = new KanBanColumn { Title = "Done", BoardId = board.Id, Position = 2, CreatedAt = now, UpdatedAt = now };

        var today = DateOnly.FromDateTime(DateTime.Today);
        DateOnly In(int days) => today.AddDays(days);

        var designTask = new TaskItem { Title = "Design KanTab UI", Priority = Priority.High, DueDate = In(5), ColumnId = todoColumn.Id, BoardId = board.Id, Position = 0, CreatedAt = now, UpdatedAt = now, Tags = new() { "Design" } };
        designTask.Checklist.Add(new ChecklistItem { Text = "Create login UI", IsCompleted = true, Position = 0, CreatedAt = now, UpdatedAt = now });
        designTask.Checklist.Add(new ChecklistItem { Text = "Connect Supabase Auth", IsCompleted = true, Position = 1, CreatedAt = now, UpdatedAt = now });
        designTask.Checklist.Add(new ChecklistItem { Text = "Add session persistence", IsCompleted = false, Position = 2, CreatedAt = now, UpdatedAt = now });
        designTask.Checklist.Add(new ChecklistItem { Text = "Test logout", IsCompleted = false, Position = 3, CreatedAt = now, UpdatedAt = now });
        designTask.Checklist.Add(new ChecklistItem { Text = "Handle authentication errors", IsCompleted = false, Position = 4, CreatedAt = now, UpdatedAt = now });
        todoColumn.Tasks.Add(designTask);
        todoColumn.Tasks.Add(new TaskItem { Title = "Write documentation", Priority = Priority.Low, DueDate = In(10), ColumnId = todoColumn.Id, BoardId = board.Id, Position = 1, CreatedAt = now.AddMinutes(1), UpdatedAt = now.AddMinutes(1), Tags = new() { "Docs" } });
        todoColumn.Tasks.Add(new TaskItem { Title = "Create mobile app", Priority = Priority.Medium, DueDate = In(15), ColumnId = todoColumn.Id, BoardId = board.Id, Position = 2, CreatedAt = now.AddMinutes(2), UpdatedAt = now.AddMinutes(2), Tags = new() { "Mobile" } });

        inProgressColumn.Tasks.Add(new TaskItem { Title = "Build Supabase integration", Priority = Priority.High, DueDate = In(3), ColumnId = inProgressColumn.Id, BoardId = board.Id, Position = 0, CreatedAt = now.AddMinutes(3), UpdatedAt = now.AddMinutes(3), Tags = new() { "Backend" } });
        inProgressColumn.Tasks.Add(new TaskItem { Title = "Implement drag and drop", Priority = Priority.Medium, ColumnId = inProgressColumn.Id, BoardId = board.Id, Position = 1, CreatedAt = now.AddMinutes(4), UpdatedAt = now.AddMinutes(4), Tags = new() { "Feature" } });

        doneColumn.Tasks.Add(new TaskItem { Title = "Set up project structure", Priority = Priority.High, ColumnId = doneColumn.Id, BoardId = board.Id, Position = 0, CreatedAt = now.AddMinutes(5), UpdatedAt = now.AddMinutes(5), IsCompleted = true, Tags = new() { "Setup" } });
        doneColumn.Tasks.Add(new TaskItem { Title = "Create desktop shell", Priority = Priority.Medium, ColumnId = doneColumn.Id, BoardId = board.Id, Position = 1, CreatedAt = now.AddMinutes(6), UpdatedAt = now.AddMinutes(6), IsCompleted = true, Tags = new() { "UI" } });

        board.Columns.Add(todoColumn);
        board.Columns.Add(inProgressColumn);
        board.Columns.Add(doneColumn);

        return new List<Board> { board };
    }

    public static Board CreateDefaultBoard(string name)
    {
        var now = DateTime.Now;
        var board = new Board
        {
            Name = name,
            Position = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        var todoColumn = new KanBanColumn { Id = Guid.NewGuid().ToString(), Title = "To Do", BoardId = board.Id, Position = 0, CreatedAt = now, UpdatedAt = now };
        var inProgressColumn = new KanBanColumn { Id = Guid.NewGuid().ToString(), Title = "In Progress", BoardId = board.Id, Position = 1, CreatedAt = now, UpdatedAt = now };
        var doneColumn = new KanBanColumn { Id = Guid.NewGuid().ToString(), Title = "Done", BoardId = board.Id, Position = 2, CreatedAt = now, UpdatedAt = now };

        board.Columns.Add(todoColumn);
        board.Columns.Add(inProgressColumn);
        board.Columns.Add(doneColumn);

        return board;
    }

    /// <summary>Creates a deep copy of a board with new IDs, preserving structure and task data.</summary>
    public static Board DuplicateBoard(Board source, string newName)
    {
        var now = DateTime.Now;
        var newBoard = new Board
        {
            Id = Guid.NewGuid().ToString(),
            Name = newName,
            Position = source.Position,
            CreatedAt = now,
            UpdatedAt = now
        };

        var columnPosition = 0;
        foreach (var sourceColumn in source.Columns)
        {
            var newColumn = new KanBanColumn
            {
                Id = Guid.NewGuid().ToString(),
                Title = sourceColumn.Title,
                BoardId = newBoard.Id,
                Position = columnPosition++,
                CreatedAt = now,
                UpdatedAt = now
            };

            var taskPosition = 0;
            foreach (var sourceTask in sourceColumn.Tasks)
            {
                var newTask = new TaskItem
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = sourceTask.Title,
                    Description = sourceTask.Description,
                    Priority = sourceTask.Priority,
                    DueDate = sourceTask.DueDate,
                    IsCompleted = sourceTask.IsCompleted,
                    Tags = new List<string>(sourceTask.Tags ?? new List<string>()),
                    ColumnId = newColumn.Id,
                    BoardId = newBoard.Id,
                    Position = taskPosition++,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                foreach (var srcItem in sourceTask.Checklist)
                {
                    newTask.Checklist.Add(new ChecklistItem
                    {
                        Id = Guid.NewGuid().ToString(),
                        Text = srcItem.Text,
                        IsCompleted = srcItem.IsCompleted,
                        Position = srcItem.Position,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
                newColumn.Tasks.Add(newTask);
            }

            newBoard.Columns.Add(newColumn);
        }

        return newBoard;
    }

    /// <summary>True when the loaded document contains nothing the user created.</summary>
    public static bool IsEmpty(KanTabData data) =>
        data.Boards.Count == 0 && data.Notes.Count == 0;
}
