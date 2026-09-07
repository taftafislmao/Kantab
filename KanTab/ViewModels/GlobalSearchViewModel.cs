using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KanTab.Models;
using KanTab.Services;

namespace KanTab.ViewModels;

public enum SearchResultKind
{
    Task,
    Note,
    Board,
    Tag
}

public abstract partial class SearchHitViewModelBase : ObservableObject
{
    public abstract SearchResultKind Kind { get; }
    [ObservableProperty] private bool _isSelected;
}

public sealed class TaskHitViewModel : SearchHitViewModelBase
{
    public TaskSearchHit Hit { get; }
    public override SearchResultKind Kind => SearchResultKind.Task;
    public string Title => Hit.Task.Title;
    public string BoardName => Hit.BoardName;
    public string ColumnName => Hit.ColumnName;
    public Priority Priority => Hit.Task.Priority;
    public string? DueDateDisplay => Hit.Task.DueDateDisplay;
    public IReadOnlyList<string> Tags => Hit.Task.Tags;
    public TaskHitViewModel(TaskSearchHit hit) => Hit = hit;
}

public sealed class NoteHitViewModel : SearchHitViewModelBase
{
    public NoteSearchHit Hit { get; }
    public override SearchResultKind Kind => SearchResultKind.Note;
    public string Title => string.IsNullOrWhiteSpace(Hit.Note.Title) ? "Untitled note" : Hit.Note.Title;
    public string Preview { get; }
    public bool IsPinned => Hit.Note.IsPinned;
    public DateTime UpdatedAt => Hit.Note.UpdatedAt;
    public NoteHitViewModel(NoteSearchHit hit)
    {
        Hit = hit;
        Preview = BuildPreview(hit.Note.Content);
    }
    private static string BuildPreview(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return string.Empty;
        var preview = string.Join(" ", content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return preview.Length <= 64 ? preview : preview[..64] + "…";
    }
}

public sealed class BoardHitViewModel : SearchHitViewModelBase
{
    public BoardSearchHit Hit { get; }
    public override SearchResultKind Kind => SearchResultKind.Board;
    public string Name => Hit.Board.Name;
    public int ColumnCount => Hit.Board.Columns.Count;
    public BoardHitViewModel(BoardSearchHit hit) => Hit = hit;
}

public sealed class TagHitViewModel : SearchHitViewModelBase
{
    public TagSearchHit Hit { get; }
    public override SearchResultKind Kind => SearchResultKind.Tag;
    public string Tag => Hit.Tag;
    public int Count => Hit.Count;
    public string Display => "#" + Hit.Tag;
    public TagHitViewModel(TagSearchHit hit) => Hit = hit;
}

public partial class GlobalSearchViewModel : ObservableObject
{
    private readonly WorkspaceState _state;
    private readonly Action<TaskItem>? _openTask;
    private readonly Action<Note>? _openNote;
    private readonly Action<Board>? _openBoard;
    private readonly Action<string>? _openTag;
    private readonly List<SearchHitViewModelBase> _flat = new();

    [ObservableProperty] private string _query = string.Empty;
    [ObservableProperty] private ObservableCollection<TaskHitViewModel> _taskHits = new();
    [ObservableProperty] private ObservableCollection<NoteHitViewModel> _noteHits = new();
    [ObservableProperty] private ObservableCollection<BoardHitViewModel> _boardHits = new();
    [ObservableProperty] private ObservableCollection<TagHitViewModel> _tagHits = new();
    [ObservableProperty] private int _selectedIndex = -1;
    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private string _noResultsText = string.Empty;

    public bool HasQuery => !string.IsNullOrWhiteSpace(Query);
    public bool HasTaskResults => TaskHits.Count > 0;
    public bool HasNoteResults => NoteHits.Count > 0;
    public bool HasBoardResults => BoardHits.Count > 0;
    public bool HasTagResults => TagHits.Count > 0;
    public bool HasAnyResults => HasTaskResults || HasNoteResults || HasBoardResults || HasTagResults;
    public bool NoResults => HasQuery && !HasAnyResults;
    public bool IsEmptyQuery => !HasQuery;
    public int TotalCount => _flat.Count;

    public event Action? RequestFocus;

    public GlobalSearchViewModel(WorkspaceState state,
        Action<TaskItem>? openTask = null,
        Action<Note>? openNote = null,
        Action<Board>? openBoard = null,
        Action<string>? openTag = null)
    {
        _state = state;
        _openTask = openTask;
        _openNote = openNote;
        _openBoard = openBoard;
        _openTag = openTag;
        _state.Changed += (_, _) => RefreshIfNeeded();
        _state.Notes.CollectionChanged += (_, _) => RefreshIfNeeded();
        _state.Boards.CollectionChanged += (_, _) => RefreshIfNeeded();
    }

    public GlobalSearchViewModel(WorkspaceState state) : this(state, null, null, null, null) { }

    partial void OnQueryChanged(string value)
    {
        PerformSearch();
        OnPropertyChanged(nameof(HasQuery));
        OnPropertyChanged(nameof(IsEmptyQuery));
    }

    private void RefreshIfNeeded()
    {
        if (HasQuery)
            PerformSearch();
    }

    private void PerformSearch()
    {
        var trimmed = Query?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            TaskHits = new ObservableCollection<TaskHitViewModel>();
            NoteHits = new ObservableCollection<NoteHitViewModel>();
            BoardHits = new ObservableCollection<BoardHitViewModel>();
            TagHits = new ObservableCollection<TagHitViewModel>();
            _flat.Clear();
            SelectedIndex = -1;
            IsOpen = false;
            NoResultsText = string.Empty;
            NotifyGroups();
            return;
        }

        var result = GlobalSearchService.Search(_state, Query);

        TaskHits = new ObservableCollection<TaskHitViewModel>(result.TaskHits.Select(h => new TaskHitViewModel(h)));
        NoteHits = new ObservableCollection<NoteHitViewModel>(result.NoteHits.Select(h => new NoteHitViewModel(h)));
        BoardHits = new ObservableCollection<BoardHitViewModel>(result.BoardHits.Select(h => new BoardHitViewModel(h)));
        TagHits = new ObservableCollection<TagHitViewModel>(result.TagHits.Select(h => new TagHitViewModel(h)));

        _flat.Clear();
        _flat.AddRange(TaskHits);
        _flat.AddRange(NoteHits);
        _flat.AddRange(BoardHits);
        _flat.AddRange(TagHits);

        if (_flat.Count > 0)
        {
            SelectedIndex = 0;
            UpdateSelection();
            IsOpen = true;
            NoResultsText = string.Empty;
        }
        else
        {
            SelectedIndex = -1;
            IsOpen = true;
            NoResultsText = $"No results for \"{trimmed}\"";
        }

        NotifyGroups();
    }

    private void NotifyGroups()
    {
        OnPropertyChanged(nameof(HasTaskResults));
        OnPropertyChanged(nameof(HasNoteResults));
        OnPropertyChanged(nameof(HasBoardResults));
        OnPropertyChanged(nameof(HasTagResults));
        OnPropertyChanged(nameof(HasAnyResults));
        OnPropertyChanged(nameof(NoResults));
        OnPropertyChanged(nameof(TotalCount));
    }

    partial void OnSelectedIndexChanged(int value)
    {
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        for (int i = 0; i < _flat.Count; i++)
            _flat[i].IsSelected = i == SelectedIndex;
    }

    public void MoveSelection(int delta)
    {
        if (_flat.Count == 0) return;
        if (SelectedIndex < 0)
        {
            SelectedIndex = delta > 0 ? 0 : _flat.Count - 1;
            return;
        }
        var next = SelectedIndex + delta;
        if (next < 0) next = _flat.Count - 1;
        if (next >= _flat.Count) next = 0;
        SelectedIndex = next;
    }

    [RelayCommand]
    private void MoveUp() => MoveSelection(-1);

    [RelayCommand]
    private void MoveDown() => MoveSelection(1);

    [RelayCommand]
    private void Clear()
    {
        Query = string.Empty;
        IsOpen = false;
        SelectedIndex = -1;
        NoResultsText = string.Empty;
    }

    [RelayCommand]
    private void Close()
    {
        IsOpen = false;
        SelectedIndex = -1;
    }

    [RelayCommand]
    private void FocusSearch()
    {
        IsOpen = HasQuery && (HasAnyResults || NoResults);
        RequestFocus?.Invoke();
    }

    public IReadOnlyList<SearchHitViewModelBase> GetFlatResults() => _flat.AsReadOnly();

    public SearchHitViewModelBase? SelectedHit => SelectedIndex >= 0 && SelectedIndex < _flat.Count ? _flat[SelectedIndex] : null;

    [RelayCommand]
    private void ExecuteSelected()
    {
        if (SelectedHit == null) return;
        ExecuteHit(SelectedHit);
    }

    public void ExecuteHit(SearchHitViewModelBase hit)
    {
        switch (hit.Kind)
        {
            case SearchResultKind.Task when hit is TaskHitViewModel t:
                _openTask?.Invoke(t.Hit.Task);
                break;
            case SearchResultKind.Note when hit is NoteHitViewModel n:
                _openNote?.Invoke(n.Hit.Note);
                break;
            case SearchResultKind.Board when hit is BoardHitViewModel b:
                _openBoard?.Invoke(b.Hit.Board);
                break;
            case SearchResultKind.Tag when hit is TagHitViewModel tg:
                _openTag?.Invoke(tg.Hit.Tag);
                break;
        }
        Query = string.Empty;
        IsOpen = false;
        SelectedIndex = -1;
    }

    [RelayCommand]
    private void OpenTaskHit(TaskHitViewModel vm) => ExecuteHit(vm);

    [RelayCommand]
    private void OpenNoteHit(NoteHitViewModel vm) => ExecuteHit(vm);

    [RelayCommand]
    private void OpenBoardHit(BoardHitViewModel vm) => ExecuteHit(vm);

    [RelayCommand]
    private void OpenTagHit(TagHitViewModel vm) => ExecuteHit(vm);

    public static string GetNotePreview(string content)
    {
        var preview = string.Join(" ", content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return preview.Length <= 64 ? preview : preview[..64] + "…";
    }
}
