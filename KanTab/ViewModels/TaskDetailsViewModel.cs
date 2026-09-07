using System;
using System.Collections.Specialized;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KanTab.Models;

namespace KanTab.ViewModels;

/// <summary>
/// Details view for a single <see cref="TaskItem"/> focused on checklist interaction.
/// Binds directly to <see cref="TaskItem.Checklist"/> (no copy) so mutations
/// apply to the canonical task instance and persist via <see cref="WorkspaceState"/>.
/// </summary>
public partial class TaskDetailsViewModel : ViewModelBase
{
    private readonly TaskItem _task;
    private readonly WorkspaceState _state;

    public TaskItem Task => _task;

    public string Title => _task.Title;
    public string Description => _task.Description;
    public bool HasDescription => !string.IsNullOrWhiteSpace(_task.Description);
    public bool HasChecklist => _task.HasChecklist;
    public string? ChecklistProgressText => _task.ChecklistProgressText;
    public string ChecklistPercentText => _task.ChecklistPercentText;
    /// <summary>Combined "2/5 • 40%" or empty when no checklist.</summary>
    public string ProgressDisplay
    {
        get
        {
            if (!HasChecklist) return string.Empty;
            var pct = ChecklistPercentText;
            var prog = ChecklistProgressText ?? string.Empty;
            if (string.IsNullOrEmpty(pct)) return prog;
            if (string.IsNullOrEmpty(prog)) return pct;
            return prog + " \u2022 " + pct;
        }
    }

    [ObservableProperty]
    private string _newChecklistText = string.Empty;

    public event EventHandler? RequestClose;

    public TaskDetailsViewModel(TaskItem task, WorkspaceState state)
    {
        _task = task;
        _state = state;
        _task.PropertyChanged += OnTaskPropertyChanged;
        _task.Checklist.CollectionChanged += OnChecklistCollectionChanged;
        foreach (var item in _task.Checklist)
            item.PropertyChanged += OnChecklistItemChanged;
    }

    private void OnTaskPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TaskItem.Title))
            OnPropertyChanged(nameof(Title));
        if (e.PropertyName is nameof(TaskItem.Description))
        {
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(HasDescription));
        }
        if (e.PropertyName is nameof(TaskItem.HasChecklist) or nameof(TaskItem.ChecklistProgressText) or nameof(TaskItem.ChecklistPercentText) or nameof(TaskItem.ChecklistCompletedCount) or nameof(TaskItem.ChecklistTotalCount))
            RefreshProgress();
    }

    private void OnChecklistCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
            foreach (ChecklistItem item in e.OldItems)
                item.PropertyChanged -= OnChecklistItemChanged;
        if (e.NewItems != null)
            foreach (ChecklistItem item in e.NewItems)
                item.PropertyChanged += OnChecklistItemChanged;
        RenumberPositions();
        RefreshProgress();
        // Any collection change is a user mutation originating from this VM.
        // Mark dirty so persistence covers it even if window close logic misses.
        TouchAndMarkDirty();
    }

    private void OnChecklistItemChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ChecklistItem.IsCompleted) or nameof(ChecklistItem.Text))
        {
            RefreshProgress();
            TouchAndMarkDirty();
        }
        else if (e.PropertyName is nameof(ChecklistItem.Position))
        {
            RefreshProgress();
        }
    }

    private void RefreshProgress()
    {
        OnPropertyChanged(nameof(HasChecklist));
        OnPropertyChanged(nameof(ChecklistProgressText));
        OnPropertyChanged(nameof(ChecklistPercentText));
        OnPropertyChanged(nameof(ProgressDisplay));
    }

    private void RenumberPositions()
    {
        for (int i = 0; i < _task.Checklist.Count; i++)
            _task.Checklist[i].Position = i;
    }

    private void TouchAndMarkDirty()
    {
        _task.UpdatedAt = DateTime.Now;
        _state.NotifyChanged();
    }

    [RelayCommand]
    private void AddChecklistItem()
    {
        var text = NewChecklistText?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return;
        var item = new ChecklistItem
        {
            Text = text,
            Position = _task.Checklist.Count,
            IsCompleted = false
        };
        _task.Checklist.Add(item);
        // CollectionChanged handler does renumber + progress + NotifyChanged
        NewChecklistText = string.Empty;
    }

    [RelayCommand]
    private void ToggleChecklistItem(ChecklistItem? item)
    {
        if (item == null) return;
        item.IsCompleted = !item.IsCompleted;
        // PropertyChanged handler will refresh + NotifyChanged
    }

    [RelayCommand]
    private void RemoveChecklistItem(ChecklistItem? item)
    {
        if (item == null) return;
        _task.Checklist.Remove(item);
    }

    [RelayCommand]
    private void MoveChecklistItemUp(ChecklistItem? item)
    {
        if (item == null) return;
        var idx = _task.Checklist.IndexOf(item);
        if (idx <= 0) return;
        _task.Checklist.Move(idx, idx - 1);
        RenumberPositions();
        RefreshProgress();
        TouchAndMarkDirty();
    }

    [RelayCommand]
    private void MoveChecklistItemDown(ChecklistItem? item)
    {
        if (item == null) return;
        var idx = _task.Checklist.IndexOf(item);
        if (idx < 0 || idx >= _task.Checklist.Count - 1) return;
        _task.Checklist.Move(idx, idx + 1);
        RenumberPositions();
        RefreshProgress();
        TouchAndMarkDirty();
    }

    [RelayCommand]
    private void Close()
    {
        Detach();
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Call when the host window is closing to clean up subscriptions.</summary>
    public void Detach()
    {
        _task.PropertyChanged -= OnTaskPropertyChanged;
        _task.Checklist.CollectionChanged -= OnChecklistCollectionChanged;
        foreach (var item in _task.Checklist.ToList())
            item.PropertyChanged -= OnChecklistItemChanged;
    }
}
