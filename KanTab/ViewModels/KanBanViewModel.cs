using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KanTab.Models;
using KanTab.Storage;
using KanTab.Views;

namespace KanTab.ViewModels;

public partial class KanBanViewModel : ViewModelBase
{
    private readonly WorkspaceState _state;

    public ObservableCollection<Board> Boards => _state.Boards;

    public Board? SelectedBoard
    {
        get => _state.SelectedBoard;
        set => _state.SelectedBoard = value;
    }

    [ObservableProperty]
    private string _searchText = string.Empty;

    public bool HasSelectedBoard => SelectedBoard != null;

    public KanBanViewModel(WorkspaceState state)
    {
        _state = state;
        _state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WorkspaceState.SelectedBoard))
            {
                OnPropertyChanged(nameof(SelectedBoard));
                OnPropertyChanged(nameof(HasSelectedBoard));
            }
        };
    }

    // ---------- Board management ----------

    [RelayCommand]
    private void CreateBoard()
    {
        var vm = new BoardDialogViewModel
        {
            IsEditMode = false,
            DialogTitle = "New Board"
        };
        var dialog = new BoardDialog(vm, "New Board");
        vm.RequestClose += (_, _) =>
        {
            if (vm.DialogResult && !string.IsNullOrWhiteSpace(vm.BoardName))
            {
                var now = DateTime.Now;
                var board = SampleDataSeeder.CreateDefaultBoard(vm.BoardName.Trim());
                board.Position = _state.Boards.Count;
                _state.Boards.Add(board);
                _state.SelectedBoard = board;
                Touch(board);
                _state.NotifyChanged();
            }
        };
        dialog.Show();
    }

    [RelayCommand]
    private void RenameBoard(Board? board)
    {
        if (board == null) return;

        var vm = new BoardDialogViewModel
        {
            IsEditMode = true,
            DialogTitle = "Rename Board",
            BoardName = board.Name
        };
        var dialog = new BoardDialog(vm, "Rename Board");
        vm.RequestClose += (_, _) =>
        {
            if (vm.DialogResult && !string.IsNullOrWhiteSpace(vm.BoardName))
            {
                board.Name = vm.BoardName.Trim();
                Touch(board);
                _state.NotifyChanged();
            }
        };
        dialog.Show();
    }

    [RelayCommand]
    private void DuplicateBoard(Board? board)
    {
        if (board == null) return;

        var vm = new BoardDialogViewModel
        {
            IsEditMode = false,
            DialogTitle = "Duplicate Board",
            BoardName = board.Name + " (Copy)"
        };
        var dialog = new BoardDialog(vm, "Duplicate Board");
        vm.RequestClose += (_, _) =>
        {
            if (vm.DialogResult && !string.IsNullOrWhiteSpace(vm.BoardName))
            {
                var copy = SampleDataSeeder.DuplicateBoard(board, vm.BoardName.Trim());
                copy.Position = _state.Boards.Count;
                _state.Boards.Add(copy);
                Touch(copy);
                _state.NotifyChanged();
            }
        };
        dialog.Show();
    }

    [RelayCommand]
    private void ArchiveBoard(Board? board)
    {
        if (board == null) return;

        var vm = new ConfirmDialogViewModel
        {
            Message = $"Archive board \"{board.Name}\"?"
        };
        var dialog = new ConfirmDialog(vm, "Archive Board");
        vm.RequestClose += (_, _) =>
        {
            if (vm.DialogResult)
            {
                board.ArchivedAt = DateTime.Now;
                board.NotifyPropertyChanged(nameof(board.IsArchived));
                Touch(board);
                _state.NotifyChanged();
            }
        };
        dialog.Show();
    }

    [RelayCommand]
    private void RestoreBoard(Board? board)
    {
        if (board == null) return;
        board.ArchivedAt = null;
        board.NotifyPropertyChanged(nameof(board.IsArchived));
        Touch(board);
        _state.NotifyChanged();
    }

    [RelayCommand]
    private void DeleteBoard(Board? board)
    {
        if (board == null) return;

        var vm = new ConfirmDialogViewModel
        {
            Message = $"Delete board \"{board.Name}\" permanently?"
        };
        var dialog = new ConfirmDialog(vm, "Delete Board");
        vm.RequestClose += (_, _) =>
        {
            if (vm.DialogResult)
            {
                _state.Boards.Remove(board);
                Touch(board);
                _state.NotifyChanged();
            }
        };
        dialog.Show();
    }

    [RelayCommand]
    private void AddTask(KanBanColumn? column)
    {
        if (SelectedBoard == null) return;

        var targetColumn = column ?? SelectedBoard.Columns.FirstOrDefault();
        if (targetColumn == null) return;

        var vm = new TaskDialogViewModel
        {
            IsEditMode = false,
            AvailableColumns = new ObservableCollection<KanBanColumn>(SelectedBoard.Columns),
            SelectedColumnId = targetColumn.Id
        };

        var dialog = new TaskDialog(vm);
        vm.RequestClose += (_, _) =>
        {
            if (vm.DialogResult)
            {
                var task = new TaskItem
                {
                    Title = vm.Title.Trim(),
                    Description = vm.Description.Trim(),
                    Priority = vm.SelectedPriority,
                    DueDate = vm.DueDateOnly,
                    DueTime = vm.DueTime,
                    ColumnId = vm.SelectedColumnId,
                    BoardId = SelectedBoard.Id,
                    Tags = new System.Collections.Generic.List<string>(vm.Tags),
                    Checklist = new ObservableCollection<ChecklistItem>(vm.Checklist.Select(c => new ChecklistItem
                    {
                        Id = c.Id,
                        Text = c.Text.Trim(),
                        IsCompleted = c.IsCompleted,
                        Position = c.Position,
                        CreatedAt = c.CreatedAt,
                        UpdatedAt = c.UpdatedAt
                    }))
                };

                var target = SelectedBoard.Columns.FirstOrDefault(c => c.Id == vm.SelectedColumnId);
                if (target != null)
                {
                    AppendTask(target, task);
                    _state.NotifyChanged();
                }
            }
        };
        dialog.Show();
    }

    [RelayCommand]
    private void EditTask(TaskItem task)
    {
        if (SelectedBoard == null) return;

        var vm = new TaskDialogViewModel
        {
            IsEditMode = true,
            Title = task.Title,
            Description = task.Description,
            SelectedPriority = task.Priority,
            DueDateOnly = task.DueDate,
            DueTime = task.DueTime,
            SelectedColumnId = task.ColumnId,
            Tags = new ObservableCollection<string>(task.Tags),
            AvailableColumns = new ObservableCollection<KanBanColumn>(SelectedBoard.Columns),
            Checklist = new ObservableCollection<ChecklistItem>(task.Checklist.Select(c => new ChecklistItem
            {
                Id = c.Id,
                Text = c.Text,
                IsCompleted = c.IsCompleted,
                Position = c.Position,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            }))
        };

        var originalColumnId = task.ColumnId;

        var dialog = new TaskDialog(vm);
        vm.RequestClose += (_, _) =>
        {
            if (vm.DialogResult)
            {
                if (originalColumnId != vm.SelectedColumnId)
                {
                    var originalColumn = SelectedBoard.Columns.FirstOrDefault(c => c.Id == originalColumnId);
                    var newColumn = SelectedBoard.Columns.FirstOrDefault(c => c.Id == vm.SelectedColumnId);
                    if (newColumn != null)
                    {
                        originalColumn?.Tasks.Remove(task);
                        AppendTask(newColumn, task);
                        RenumberTasks(originalColumn);
                    }
                }

                task.Title = vm.Title.Trim();
                task.Description = vm.Description.Trim();
                task.Priority = vm.SelectedPriority;
                task.DueDate = vm.DueDateOnly;
                task.DueTime = vm.DueTime;
                task.ColumnId = vm.SelectedColumnId;
                task.Tags = new System.Collections.Generic.List<string>(vm.Tags);
                task.Checklist = new ObservableCollection<ChecklistItem>(vm.Checklist.Select(c => new ChecklistItem
                {
                    Id = c.Id,
                    Text = c.Text.Trim(),
                    IsCompleted = c.IsCompleted,
                    Position = c.Position,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt
                }));
                Touch(task);
                _state.NotifyChanged();
            }
        };
        dialog.Show();
    }

     private readonly Dictionary<string, TaskDetailsWindow> _openDetailsWindows = new();

    /// <summary>
    /// Idempotent: exactly one Details window per task id. If already open, focuses the existing window.
    /// This is the sole authority for opening Details; both ClickCount==2 and DoubleTapped must converge here.
    /// </summary>
    public void OpenTaskDetails(TaskItem task)
    {
        if (_openDetailsWindows.TryGetValue(task.Id, out var existing))
        {
            try
            {
                if (existing.IsVisible)
                {
                    existing.Activate();
                    if (existing.WindowState == Avalonia.Controls.WindowState.Minimized)
                        existing.WindowState = Avalonia.Controls.WindowState.Normal;
                    existing.Focus();
                    return;
                }
            }
            catch { }
            _openDetailsWindows.Remove(task.Id);
        }

        var vm = new TaskDetailsViewModel(task, _state);
        var window = new TaskDetailsWindow(vm);
        _openDetailsWindows[task.Id] = window;
        window.Closed += (_, _) => _openDetailsWindows.Remove(task.Id);
        window.Show();
    }

    internal bool IsTaskDetailsOpen(string taskId) => _openDetailsWindows.ContainsKey(taskId);
    internal int OpenTaskDetailsCount => _openDetailsWindows.Count;

    [RelayCommand]
    private void DeleteTask(TaskItem task)
    {
        if (SelectedBoard == null) return;

        var wasRemoved = false;
        foreach (var column in SelectedBoard.Columns)
        {
            wasRemoved |= column.Tasks.Remove(task);
            RenumberTasks(column);
        }

        if (wasRemoved)
        {
            Touch(SelectedBoard);
            _state.NotifyChanged();
        }
    }

    [RelayCommand]
    private void MoveTask((TaskItem task, string targetColumnId) args)
    {
        if (SelectedBoard == null) return;

        var (task, targetColumnId) = args;

        if (task.ColumnId == targetColumnId) return;

        var destColumn = SelectedBoard.Columns.FirstOrDefault(c => c.Id == targetColumnId);
        if (destColumn != null)
        {
            var sourceColumn = SelectedBoard.Columns.FirstOrDefault(c => c.Id == task.ColumnId);
            sourceColumn?.Tasks.Remove(task);
            AppendTask(destColumn, task);
            RenumberTasks(sourceColumn);
            _state.NotifyChanged();
        }
    }

    [RelayCommand]
    private void AddColumn()
    {
        if (SelectedBoard == null) return;

        var vm = new ColumnDialogViewModel { IsEditMode = false };
        var dialog = new ColumnDialog(vm, "New Column");
        vm.RequestClose += (_, _) =>
        {
            if (vm.DialogResult && !string.IsNullOrWhiteSpace(vm.ColumnName))
            {
                var now = DateTime.Now;
                var column = new KanBanColumn
                {
                    Title = vm.ColumnName.Trim(),
                    BoardId = SelectedBoard.Id,
                    Position = SelectedBoard.Columns.Count,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                SelectedBoard.Columns.Add(column);
                Touch(SelectedBoard);
                _state.NotifyChanged();
            }
        };
        dialog.Show();
    }

    [RelayCommand]
    private void RenameColumn(KanBanColumn column)
    {
        if (SelectedBoard == null || column == null) return;

        var vm = new ColumnDialogViewModel
        {
            IsEditMode = true,
            ColumnName = column.Title
        };
        var dialog = new ColumnDialog(vm, "Rename Column");
        vm.RequestClose += (_, _) =>
        {
            if (vm.DialogResult && !string.IsNullOrWhiteSpace(vm.ColumnName))
            {
                column.Title = vm.ColumnName.Trim();
                Touch(column);
                Touch(SelectedBoard);
                _state.NotifyChanged();
            }
        };
        dialog.Show();
    }

    [RelayCommand]
    private void DeleteColumn(KanBanColumn column)
    {
        if (SelectedBoard == null || column == null) return;

        if (column.Tasks.Count > 0)
        {
            var blockedVm = new ConfirmDialogViewModel
            {
                Message = "This column contains tasks.\n\nMove or delete its tasks before deleting the column."
            };
            var blockedDialog = new ConfirmDialog(blockedVm, "Cannot Delete Column");
            blockedDialog.Show();
            return;
        }

        var vm = new ConfirmDialogViewModel
        {
            Message = $"Delete column \"{column.Title}\"?"
        };
        var dialog = new ConfirmDialog(vm, "Delete Column");
        vm.RequestClose += (_, _) =>
        {
            if (vm.DialogResult)
            {
                SelectedBoard.Columns.Remove(column);
                RenumberColumns(SelectedBoard);
                Touch(SelectedBoard);
                _state.NotifyChanged();
            }
        };
        dialog.Show();
    }

    // ---------- ordering / timestamp helpers ----------

    private static void AppendTask(KanBanColumn column, TaskItem task)
    {
        task.ColumnId = column.Id;
        task.Position = column.Tasks.Count == 0 ? 0 : column.Tasks.Max(t => t.Position) + 1;
        column.Tasks.Add(task);
        Touch(column);
    }

    private static void RenumberTasks(KanBanColumn? column)
    {
        if (column == null) return;
        var position = 0;
        foreach (var task in column.Tasks)
            task.Position = position++;
    }

    private static void RenumberColumns(Board board)
    {
        var position = 0;
        foreach (var c in board.Columns)
            c.Position = position++;
    }

    private static void Touch(TaskItem task) => task.UpdatedAt = DateTime.Now;

    private static void Touch(KanBanColumn column) => column.UpdatedAt = DateTime.Now;

    private static void Touch(Board? board)
    {
        if (board != null)
            board.UpdatedAt = DateTime.Now;
    }

    public void CreateTaskWithDueDate(DateOnly dueDate)
    {
        if (SelectedBoard == null) return;

        var targetColumn = SelectedBoard.Columns.FirstOrDefault();
        if (targetColumn == null) return;

        var vm = new TaskDialogViewModel
        {
            IsEditMode = false,
            AvailableColumns = new ObservableCollection<KanBanColumn>(SelectedBoard.Columns),
            SelectedColumnId = targetColumn.Id,
            DueDateOnly = dueDate
        };

        var dialog = new TaskDialog(vm);
        vm.RequestClose += (_, _) =>
        {
            if (vm.DialogResult)
            {
                var task = new TaskItem
                {
                    Title = vm.Title.Trim(),
                    Description = vm.Description.Trim(),
                    Priority = vm.SelectedPriority,
                    DueDate = vm.DueDateOnly,
                    DueTime = vm.DueTime,
                    ColumnId = vm.SelectedColumnId,
                    BoardId = SelectedBoard.Id,
                    Tags = new System.Collections.Generic.List<string>(vm.Tags),
                    Checklist = new ObservableCollection<ChecklistItem>(vm.Checklist.Select(c => new ChecklistItem
                    {
                        Id = c.Id,
                        Text = c.Text.Trim(),
                        IsCompleted = c.IsCompleted,
                        Position = c.Position,
                        CreatedAt = c.CreatedAt,
                        UpdatedAt = c.UpdatedAt
                    }))
                };

                var target = SelectedBoard.Columns.FirstOrDefault(c => c.Id == vm.SelectedColumnId);
                if (target != null)
                {
                    AppendTask(target, task);
                    _state.NotifyChanged();
                }
            }
        };
        dialog.Show();
    }
}
