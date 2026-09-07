using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KanTab.Models;
using KanTab.Views;

namespace KanTab.ViewModels;

public partial class NotesViewModel : ViewModelBase
{
    private readonly DispatcherTimer _saveTimer;
    private readonly WorkspaceState _state;
    private bool _isCreatingNote;

    public ObservableCollection<Note> Notes => _state.Notes;

    [ObservableProperty]
    private ObservableCollection<NoteListItemViewModel> _filteredNotes = new();

    [ObservableProperty]
    private Note? _selectedNote;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _saveStatus = "Saved";

    [ObservableProperty]
    private string? _storageErrorMessage;

    [ObservableProperty]
    private bool _isCompactLayout;

    [ObservableProperty]
    private bool _isEditorOpen;

    public bool HasSelectedNote => SelectedNote != null;
    public bool HasNoSelectedNote => SelectedNote == null;
    public bool HasNotes => Notes.Count > 0;
    public bool HasNotesToShow => FilteredNotes.Count > 0;
    public bool HasNoNotes => Notes.Count == 0;
    public bool HasNoSearchMatches => Notes.Count > 0 && FilteredNotes.Count == 0;
    public bool HasSearchText => !string.IsNullOrWhiteSpace(SearchText);
    public bool IsListPaneVisible => !IsCompactLayout || !IsEditorOpen;
    public bool IsEditorPaneVisible => !IsCompactLayout || IsEditorOpen;
    public string PinButtonText => SelectedNote?.IsPinned == true ? "Unpin" : "Pin";
    public string LastUpdatedText => SelectedNote == null
        ? string.Empty
        : $"Last updated {FormatDate(SelectedNote.UpdatedAt)}";

    public NotesViewModel(WorkspaceState state)
    {
        _state = state;
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            SaveNow();
        };

        Notes.CollectionChanged += (_, _) =>
        {
            RefreshNotesList();
            NotifyCollectionStateChanged();
        };

        if (!string.IsNullOrWhiteSpace(_state.LoadError))
            StorageErrorMessage = "Notes could not be loaded. A new empty notes list is being used.";

        RefreshNotesList();
        if (Notes.Count > 0)
            SelectedNote = GetVisibleNotes().FirstOrDefault() ?? GetOrderedNotes().FirstOrDefault();
    }

    partial void OnSelectedNoteChanged(Note? oldValue, Note? newValue)
    {
        if (oldValue != null)
            oldValue.PropertyChanged -= OnSelectedNotePropertyChanged;
        if (newValue != null)
            newValue.PropertyChanged += OnSelectedNotePropertyChanged;

        IsEditorOpen = IsCompactLayout ? newValue != null : true;

        OnPropertyChanged(nameof(HasSelectedNote));
        OnPropertyChanged(nameof(HasNoSelectedNote));
        OnPropertyChanged(nameof(PinButtonText));
        OnPropertyChanged(nameof(LastUpdatedText));
        OnPropertyChanged(nameof(IsListPaneVisible));
        OnPropertyChanged(nameof(IsEditorPaneVisible));
        RefreshNotesList();
    }

    partial void OnSearchTextChanged(string value)
    {
        RefreshNotesList();
        OnPropertyChanged(nameof(HasSearchText));
    }

    partial void OnIsCompactLayoutChanged(bool value)
    {
        IsEditorOpen = value ? SelectedNote != null : true;
        OnPropertyChanged(nameof(IsListPaneVisible));
        OnPropertyChanged(nameof(IsEditorPaneVisible));
    }

    partial void OnIsEditorOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(IsListPaneVisible));
        OnPropertyChanged(nameof(IsEditorPaneVisible));
    }

    private void OnSelectedNotePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not Note note)
            return;

        if (e.PropertyName is nameof(Note.Title) or nameof(Note.Content) or nameof(Note.IsPinned))
        {
            note.UpdatedAt = DateTime.Now;
            QueueSave();
            RefreshNotesList();
        }

        if (ReferenceEquals(note, SelectedNote) &&
            e.PropertyName is nameof(Note.IsPinned) or nameof(Note.UpdatedAt))
        {
            OnPropertyChanged(nameof(PinButtonText));
            OnPropertyChanged(nameof(LastUpdatedText));
        }
    }

    private void RefreshNotesList()
    {
        var matchingNotes = GetVisibleNotes();
        var selectedId = SelectedNote?.Id;
        FilteredNotes = new ObservableCollection<NoteListItemViewModel>(
            matchingNotes.Select(note => new NoteListItemViewModel(note, note.Id == selectedId)));

        NotifyCollectionStateChanged();
    }

    private List<Note> GetVisibleNotes()
    {
        var search = SearchText.Trim();
        return FilterNotes(OrderNotes(Notes), search).ToList();
    }

    private List<Note> GetOrderedNotes() => OrderNotes(Notes).ToList();

    // Pure helpers (no UI dependency) so the sorting/filtering rules can be unit tested.
    internal static IEnumerable<Note> OrderNotes(IEnumerable<Note> notes) =>
        notes.OrderByDescending(note => note.IsPinned).ThenByDescending(note => note.UpdatedAt);

    internal static IEnumerable<Note> FilterNotes(IEnumerable<Note> notes, string search) =>
        string.IsNullOrWhiteSpace(search)
            ? notes
            : notes.Where(note =>
                note.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                note.Content.Contains(search, StringComparison.OrdinalIgnoreCase));

    private void NotifyCollectionStateChanged()
    {
        OnPropertyChanged(nameof(HasNotes));
        OnPropertyChanged(nameof(HasNotesToShow));
        OnPropertyChanged(nameof(HasNoNotes));
        OnPropertyChanged(nameof(HasNoSearchMatches));
    }

    private void QueueSave()
    {
        SaveStatus = "Saving…";
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveNow()
    {
        _saveTimer.Stop();
        if (_state.SaveNow())
        {
            SaveStatus = "Saved";
            StorageErrorMessage = null;
        }
        else
        {
            SaveStatus = "Save failed";
            StorageErrorMessage = "Changes are kept in memory, but notes could not be saved.";
        }
    }

    public void FlushPendingSave() => SaveNow();

    [RelayCommand]
    private void NewNote()
    {
        if (_isCreatingNote)
            return;

        // A rapid double click should focus the draft already created rather than
        // producing a stack of indistinguishable empty notes.
        var existingDraft = Notes.FirstOrDefault(note =>
            note.Title == "Untitled note" &&
            string.IsNullOrWhiteSpace(note.Content) &&
            DateTime.Now - note.CreatedAt < TimeSpan.FromSeconds(2));
        if (existingDraft != null)
        {
            SelectedNote = existingDraft;
            return;
        }

        _isCreatingNote = true;
        try
        {
            var now = DateTime.Now;
            var note = new Note
            {
                Title = "Untitled note",
                CreatedAt = now,
                UpdatedAt = now
            };
            Notes.Insert(0, note);
            SelectedNote = note;
            SaveNow();
        }
        finally
        {
            _isCreatingNote = false;
        }
    }

    [RelayCommand]
    private void SelectNote(Note note)
    {
        if (note != null)
            SelectedNote = note;
    }

    [RelayCommand]
    private void TogglePin()
    {
        if (SelectedNote == null)
            return;

        SelectedNote.IsPinned = !SelectedNote.IsPinned;
        SaveNow();
    }

    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

    [RelayCommand]
    private void BackToList() => IsEditorOpen = false;

    [RelayCommand]
    private void DeleteCurrentNote()
    {
        if (SelectedNote != null)
            DeleteNote(SelectedNote);
    }

    [RelayCommand]
    private void DeleteNote(Note note)
    {
        if (note == null)
            return;

        var wasSelected = ReferenceEquals(note, SelectedNote);
        var visibleBeforeDelete = GetVisibleNotes();
        var deletedIndex = visibleBeforeDelete.FindIndex(candidate => candidate.Id == note.Id);
        var nextNote = wasSelected
            ? visibleBeforeDelete.Where(candidate => candidate.Id != note.Id).ElementAtOrDefault(Math.Max(0, deletedIndex))
              ?? visibleBeforeDelete.LastOrDefault(candidate => candidate.Id != note.Id)
              ?? GetOrderedNotes().FirstOrDefault(candidate => candidate.Id != note.Id)
            : null;

        var dialogViewModel = new ConfirmDialogViewModel
        {
            Message = $"Delete note \"{(string.IsNullOrWhiteSpace(note.Title) ? "Untitled note" : note.Title)}\"?"
        };
        var dialog = new ConfirmDialog(dialogViewModel, "Delete Note");
        dialogViewModel.RequestClose += (_, _) =>
        {
            if (!dialogViewModel.DialogResult)
                return;

            Notes.Remove(note);
            if (wasSelected)
                SelectedNote = nextNote;
            SaveNow();
        };
        dialog.Show();
    }

    private static string FormatDate(DateTime date)
    {
        if (date.Date == DateTime.Today)
            return $"today at {date:h:mm tt}";
        if (date.Date == DateTime.Today.AddDays(-1))
            return $"yesterday at {date:h:mm tt}";
        return date.ToString("MMM d, yyyy h:mm tt");
    }
}

public partial class NoteListItemViewModel : ObservableObject
{
    public Note Note { get; }
    public string Title => string.IsNullOrWhiteSpace(Note.Title) ? "Untitled note" : Note.Title;
    public string Preview => BuildPreview(Note.Content);
    public string UpdatedLabel => FormatDate(Note.UpdatedAt);
    public bool IsPinned => Note.IsPinned;

    [ObservableProperty]
    private bool _isSelected;

    public NoteListItemViewModel(Note note, bool isSelected)
    {
        Note = note;
        IsSelected = isSelected;
    }

    private static string BuildPreview(string content)
    {
        var preview = string.Join(" ", content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return preview.Length <= 96 ? preview : preview[..96] + "…";
    }

    private static string FormatDate(DateTime date) =>
        date.Date == DateTime.Today ? date.ToString("h:mm tt") : date.ToString("MMM d");
}
