using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KanTab.Models;

namespace KanTab.ViewModels;

public partial class TaskDialogViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private Priority _selectedPriority = Priority.Medium;

    [ObservableProperty]
    private DateOnly? _dueDateOnly;

    [ObservableProperty]
    private TimeSpan? _dueTime;

    [ObservableProperty]
    private string? _dueDate;

    [ObservableProperty]
    private string _selectedColumnId = string.Empty;

    [ObservableProperty]
    private string _tagInput = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _tags = new();

    [ObservableProperty]
    private ObservableCollection<KanBanColumn> _availableColumns = new();

    [ObservableProperty]
    private string? _validationMessage;

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private bool _dialogResult;

    [ObservableProperty]
    private string _newChecklistText = string.Empty;

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
                    item.PropertyChanged -= OnChecklistItemChanged;
            }
            if (SetProperty(ref _checklist, value ?? new ObservableCollection<ChecklistItem>()))
            {
                _checklist.CollectionChanged += OnChecklistCollectionChanged;
                foreach (var item in _checklist)
                    item.PropertyChanged += OnChecklistItemChanged;
                RefreshChecklistProgress();
            }
        }
    }

    public TaskDialogViewModel()
    {
        _checklist.CollectionChanged += OnChecklistCollectionChanged;
    }

    public Priority[] Priorities => new[] { Priority.Low, Priority.Medium, Priority.High };

    public bool CanCreate => !string.IsNullOrWhiteSpace(Title);

    public string PrimaryActionText => IsEditMode ? "Save" : "Create";

    partial void OnIsEditModeChanged(bool value)
    {
        OnPropertyChanged(nameof(PrimaryActionText));
    }

    public bool HasChecklist => Checklist.Count > 0;

    public string ChecklistProgressText => HasChecklist ? Checklist.Count(c => c.IsCompleted) + "/" + Checklist.Count : string.Empty;

    public string ChecklistPercentText
    {
        get
        {
            if (!HasChecklist) return string.Empty;
            var pct = (double)Checklist.Count(c => c.IsCompleted) / Checklist.Count * 100;
            return ((int)Math.Round(pct)).ToString() + "%";
        }
    }

    private void OnChecklistCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
            foreach (ChecklistItem item in e.OldItems)
                item.PropertyChanged -= OnChecklistItemChanged;
        if (e.NewItems != null)
            foreach (ChecklistItem item in e.NewItems)
                item.PropertyChanged += OnChecklistItemChanged;
        RefreshChecklistProgress();
        RenumberChecklistPositions();
    }

    private void OnChecklistItemChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChecklistItem.IsCompleted) || e.PropertyName == nameof(ChecklistItem.Text))
        {
            RefreshChecklistProgress();
        }
    }

    private void RefreshChecklistProgress()
    {
        OnPropertyChanged(nameof(HasChecklist));
        OnPropertyChanged(nameof(ChecklistProgressText));
        OnPropertyChanged(nameof(ChecklistPercentText));
    }

    private void RenumberChecklistPositions()
    {
        for (int i = 0; i < Checklist.Count; i++)
            Checklist[i].Position = i;
    }

    [RelayCommand]
    private void AddTag()
    {
        if (!string.IsNullOrWhiteSpace(TagInput))
        {
            Tags.Add(TagInput.Trim());
            TagInput = string.Empty;
        }
    }

    [RelayCommand]
    private void RemoveTag(string tag)
    {
        Tags.Remove(tag);
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
            Position = Checklist.Count,
            IsCompleted = false
        };
        Checklist.Add(item);
        NewChecklistText = string.Empty;
    }

    [RelayCommand]
    private void RemoveChecklistItem(ChecklistItem? item)
    {
        if (item == null) return;
        Checklist.Remove(item);
    }

    [RelayCommand]
    private void MoveChecklistItemUp(ChecklistItem? item)
    {
        if (item == null) return;
        var idx = Checklist.IndexOf(item);
        if (idx <= 0) return;
        Checklist.Move(idx, idx - 1);
        RenumberChecklistPositions();
        RefreshChecklistProgress();
    }

    [RelayCommand]
    private void MoveChecklistItemDown(ChecklistItem? item)
    {
        if (item == null) return;
        var idx = Checklist.IndexOf(item);
        if (idx < 0 || idx >= Checklist.Count - 1) return;
        Checklist.Move(idx, idx + 1);
        RenumberChecklistPositions();
        RefreshChecklistProgress();
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResult = false;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Create()
    {
        if (!CanCreate)
        {
            ValidationMessage = "Title is required.";
            return;
        }

        ValidationMessage = null;
        DialogResult = true;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? RequestClose;
}
