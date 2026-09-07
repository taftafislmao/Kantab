using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace KanTab.Models;

public partial class TaskItem : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string BoardId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public int Position { get; set; }

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private Priority _priority = Priority.Medium;

    [ObservableProperty]
    private DateOnly? _dueDate;

    [ObservableProperty]
    private TimeSpan? _dueTime;

    [ObservableProperty]
    private string _columnId = string.Empty;

    [ObservableProperty]
    private bool _isCompleted;

    [ObservableProperty]
    private List<string> _tags = new();

    private ObservableCollection<ChecklistItem> _checklist = new();

    public ObservableCollection<ChecklistItem> Checklist
    {
        get => _checklist;
        set
        {
            if (_checklist != null)
            {
                _checklist.CollectionChanged -= OnChecklistCollectionChanged;
                foreach (var item in _checklist)
                    item.PropertyChanged -= OnChecklistItemPropertyChanged;
            }
            _checklist = value ?? new ObservableCollection<ChecklistItem>();
            _checklist.CollectionChanged += OnChecklistCollectionChanged;
            foreach (var item in _checklist)
                item.PropertyChanged += OnChecklistItemPropertyChanged;
            RefreshChecklistProperties();
        }
    }

    public TaskItem()
    {
        _checklist.CollectionChanged += OnChecklistCollectionChanged;
    }

    public bool HasChecklist => Checklist.Count > 0;
    public int ChecklistCompletedCount => Checklist.Count(c => c.IsCompleted);
    public int ChecklistTotalCount => Checklist.Count;
    public string? ChecklistProgressText => HasChecklist ? ChecklistCompletedCount + "/" + ChecklistTotalCount : null;
    public double ChecklistPercent => ChecklistTotalCount == 0 ? 0 : (double)ChecklistCompletedCount / ChecklistTotalCount * 100;
    public string ChecklistPercentText => HasChecklist ? ((int)Math.Round(ChecklistPercent)).ToString() + "%" : string.Empty;
    public bool IsChecklistComplete => HasChecklist && ChecklistCompletedCount == ChecklistTotalCount;

    private void OnChecklistCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
            foreach (ChecklistItem item in e.OldItems)
                item.PropertyChanged -= OnChecklistItemPropertyChanged;
        if (e.NewItems != null)
            foreach (ChecklistItem item in e.NewItems)
                item.PropertyChanged += OnChecklistItemPropertyChanged;
        RefreshChecklistProperties();
        UpdatedAt = DateTime.Now;
    }

    private void OnChecklistItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChecklistItem.IsCompleted) || e.PropertyName == nameof(ChecklistItem.Text) || e.PropertyName == nameof(ChecklistItem.Position))
        {
            RefreshChecklistProperties();
            UpdatedAt = DateTime.Now;
        }
    }

    private void RefreshChecklistProperties()
    {
        OnPropertyChanged(nameof(HasChecklist));
        OnPropertyChanged(nameof(ChecklistCompletedCount));
        OnPropertyChanged(nameof(ChecklistTotalCount));
        OnPropertyChanged(nameof(ChecklistProgressText));
        OnPropertyChanged(nameof(ChecklistPercent));
        OnPropertyChanged(nameof(ChecklistPercentText));
        OnPropertyChanged(nameof(IsChecklistComplete));
    }

    /// <summary>Local due instant for notifications/sorting. DueTime defaults to midnight.</summary>
    public DateTime? DueDateTime => DueDate == null ? null : DueDate.Value.ToDateTime(DueTime == null ? TimeOnly.MinValue : TimeOnly.FromTimeSpan(DueTime.Value), DateTimeKind.Local);

    public string? DueDateDisplay
    {
        get
        {
            if (DueDate == null) return null;
            var s = DueDate.Value.ToString("MMM d");
            if (DueTime != null) s += $" {TimeOnly.FromTimeSpan(DueTime.Value):h:mm tt}";
            return s;
        }
    }

    public string DueDateRelativeDisplay
    {
        get
        {
            if (DueDate == null)
                return "No due date";

            var today = DateOnly.FromDateTime(DateTime.Today);
            var diff = DueDate.Value.DayNumber - today.DayNumber;

            if (diff < 0)
                return "Overdue";
            if (diff == 0)
                return DueTime == null ? "Today" : $"Today {TimeOnly.FromTimeSpan(DueTime.Value):h:mm tt}";
            if (diff == 1)
                return DueTime == null ? "Tomorrow" : $"Tomorrow {TimeOnly.FromTimeSpan(DueTime.Value):h:mm tt}";
            if (diff <= 7)
                return $"This week ({DueDate?.ToString("MMM d")})";
            return DueDate?.ToString("MMM d, yyyy") ?? "No due date";
        }
    }

    public bool IsOverdue
    {
        get
        {
            if (DueDate == null || IsCompleted) return false;
            var dt = DueDateTime;
            return dt != null && dt.Value < DateTime.Now;
        }
    }

    public bool IsDueToday
    {
        get
        {
            if (DueDate == null || IsCompleted) return false;
            return DueDate.Value == DateOnly.FromDateTime(DateTime.Today);
        }
    }

    partial void OnDueDateChanged(DateOnly? value)
    {
        OnPropertyChanged(nameof(DueDateDisplay));
        OnPropertyChanged(nameof(DueDateRelativeDisplay));
        OnPropertyChanged(nameof(DueDateTime));
        OnPropertyChanged(nameof(IsOverdue));
        OnPropertyChanged(nameof(IsDueToday));
    }

    partial void OnDueTimeChanged(TimeSpan? value)
    {
        OnPropertyChanged(nameof(DueDateDisplay));
        OnPropertyChanged(nameof(DueDateRelativeDisplay));
        OnPropertyChanged(nameof(DueDateTime));
        OnPropertyChanged(nameof(IsOverdue));
        OnPropertyChanged(nameof(IsDueToday));
    }

    partial void OnIsCompletedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsOverdue));
        OnPropertyChanged(nameof(IsDueToday));
    }
}
