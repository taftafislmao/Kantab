using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KanTab.Models;

namespace KanTab.ViewModels;

public partial class TasksViewModel : ViewModelBase
{
    private readonly KanBanViewModel _kanBan;
    private readonly WorkspaceState _state;

    public KanBanViewModel KanBan => _kanBan;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedFilter = "All";

    [ObservableProperty]
    private string _selectedSort = "Newest";

    [ObservableProperty]
    private ObservableCollection<TaskRowViewModel> _rows = new();

    public bool IsEmpty => Rows.Count == 0;

    public string[] Filters => new[] { "All", "Active", "Completed", "Overdue", "Due Today", "No Due Date" };
    public string[] Sorts => new[] { "Newest", "Oldest", "Due date", "Priority", "Overdue first" };

    public TasksViewModel(WorkspaceState state) : this(state, new KanBanViewModel(state))
    {
    }

    public TasksViewModel(WorkspaceState state, KanBanViewModel kanBan)
    {
        _state = state;
        _kanBan = kanBan;
        _kanBan.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(KanBanViewModel.SelectedBoard))
                RefreshRows();
        };
        _state.Changed += OnBoardChanged;
        RefreshRows();
    }

    partial void OnSearchTextChanged(string value) => RefreshRows();
    partial void OnSelectedFilterChanged(string value) => RefreshRows();
    partial void OnSelectedSortChanged(string value) => RefreshRows();

    private void OnBoardChanged(object? sender, EventArgs e) => RefreshRows();

    private void RefreshRows()
    {
        var board = _kanBan.SelectedBoard;
        if (board == null) return;

        foreach (var row in Rows)
        {
            if (row is IDisposable disposable)
                disposable.Dispose();
        }

        var rows = new List<TaskRowViewModel>();

        foreach (var column in board.Columns)
        {
            foreach (var task in column.Tasks)
            {
                rows.Add(new TaskRowViewModel(task, column.Title, _state));
            }
        }

        rows = SelectedFilter switch
        {
            "Active" => rows.Where(r => !r.Task.IsCompleted).ToList(),
            "Completed" => rows.Where(r => r.Task.IsCompleted).ToList(),
            "Overdue" => rows.Where(r => r.Task.IsOverdue).ToList(),
            "Due Today" => rows.Where(r => r.Task.IsDueToday).ToList(),
            "No Due Date" => rows.Where(r => !r.Task.DueDate.HasValue).ToList(),
            _ => rows
        };

        var search = SearchText?.Trim() ?? string.Empty;
        if (search.Length > 0)
        {
            rows = rows.Where(r =>
                r.Task.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                r.Task.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                r.Task.Tags.Any(t => t.Contains(search, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

        rows = SelectedSort switch
        {
            "Oldest" => rows.OrderBy(r => r.Task.CreatedAt).ToList(),
            "Due date" => rows.OrderBy(r => r.Task.DueDateTime == null).ThenBy(r => r.Task.DueDateTime).ToList(),
            "Priority" => rows.OrderBy(r => r.Task.Priority).ToList(),
            "Overdue first" => rows.OrderBy(r => r.Task.IsOverdue ? 0 : (r.Task.DueDate.HasValue ? 1 : 2)).ThenBy(r => r.Task.DueDateTime).ToList(),
            _ => rows.OrderByDescending(r => r.Task.CreatedAt).ToList()
        };

        Rows = new ObservableCollection<TaskRowViewModel>(rows);
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private void AddTask() => _kanBan.AddTaskCommand.Execute(null);

    [RelayCommand]
    private void EditTask(TaskRowViewModel row) => _kanBan.EditTaskCommand.Execute(row.Task);

    public void OpenTaskDetails(TaskRowViewModel row) => _kanBan.OpenTaskDetails(row.Task);

    [RelayCommand]
    private void DeleteTask(TaskRowViewModel row)
    {
        _kanBan.DeleteTaskCommand.Execute(row.Task);
        RefreshRows();
    }
}

public partial class TaskRowViewModel : ObservableObject, IDisposable
{
    public TaskItem Task { get; }

    public string Title => Task.Title;
    public string Priority => Task.Priority.ToString();
    public string? DueDate => Task.DueDateDisplay;
    public bool IsOverdue => Task.IsOverdue;
    public bool IsDueToday => Task.IsDueToday;
    public string ColumnTitle { get; }
    public IReadOnlyList<string> Tags => Task.Tags;
    public bool HasChecklist => Task.HasChecklist;
    public string? ChecklistProgressText => Task.ChecklistProgressText;
    public double ChecklistPercent => Task.ChecklistPercent;

    [ObservableProperty]
    private bool _isCompleted;

    private readonly WorkspaceState _state;

    public TaskRowViewModel(TaskItem task, string columnTitle, WorkspaceState state)
    {
        Task = task;
        ColumnTitle = columnTitle;
        _state = state;
        _isCompleted = task.IsCompleted;
        SubscribeToTask();
    }

    private void SubscribeToTask()
    {
        Task.PropertyChanged += OnTaskPropertyChanged;
        if (Task.Checklist != null)
            Task.Checklist.CollectionChanged += OnChecklistChanged;
        foreach (var item in Task.Checklist)
            item.PropertyChanged += OnChecklistItemChanged;
    }

    private void UnsubscribeFromTask()
    {
        Task.PropertyChanged -= OnTaskPropertyChanged;
        if (Task.Checklist != null)
            Task.Checklist.CollectionChanged -= OnChecklistChanged;
        foreach (var item in Task.Checklist)
            item.PropertyChanged -= OnChecklistItemChanged;
    }

    private void OnChecklistChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
            foreach (ChecklistItem item in e.OldItems)
                item.PropertyChanged -= OnChecklistItemChanged;
        if (e.NewItems != null)
            foreach (ChecklistItem item in e.NewItems)
                item.PropertyChanged += OnChecklistItemChanged;
        OnPropertyChanged(nameof(HasChecklist));
        OnPropertyChanged(nameof(ChecklistProgressText));
        OnPropertyChanged(nameof(ChecklistPercent));
    }

    private void OnChecklistItemChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChecklistItem.IsCompleted))
        {
            OnPropertyChanged(nameof(ChecklistProgressText));
            OnPropertyChanged(nameof(ChecklistPercent));
            OnPropertyChanged(nameof(HasChecklist));
        }
    }

    private void OnTaskPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TaskItem.IsCompleted))
            OnPropertyChanged(nameof(IsCompleted));
        if (e.PropertyName is nameof(TaskItem.HasChecklist) or nameof(TaskItem.ChecklistProgressText) or nameof(TaskItem.ChecklistPercent) or nameof(TaskItem.ChecklistCompletedCount) or nameof(TaskItem.ChecklistTotalCount))
        {
            OnPropertyChanged(nameof(HasChecklist));
            OnPropertyChanged(nameof(ChecklistProgressText));
            OnPropertyChanged(nameof(ChecklistPercent));
        }
        if (e.PropertyName is nameof(TaskItem.Title))
            OnPropertyChanged(nameof(Title));
    }

    partial void OnIsCompletedChanged(bool value)
    {
        Task.IsCompleted = value;
        Task.UpdatedAt = DateTime.Now;
        _state.NotifyChanged();
    }

    public void Dispose()
    {
        UnsubscribeFromTask();
    }
}
