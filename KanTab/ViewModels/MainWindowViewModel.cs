using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KanTab.Models;
using KanTab.Storage;
using KanTab.Storage.Supabase;

namespace KanTab.ViewModels;

/// <summary>A single sidebar navigation entry.</summary>
public class NavItem
{
    public required string Label { get; init; }
    public required IRelayCommand Command { get; init; }
}

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly WorkspaceState _state;
    private readonly KanBanViewModel _kanBanViewModel;
    private readonly TasksViewModel _tasksViewModel;
    private readonly NotesViewModel _notesViewModel;
    private readonly IAuthService? _authService;
    private readonly ICloudKanTabRepository? _cloudRepository;
    private readonly ISyncService? _syncService;

    [ObservableProperty]
    private ViewModelBase? _currentSection;

    [ObservableProperty]
    private NavItem? _selectedNavItem;

    public ObservableCollection<NavItem> NavItems { get; } = new();

    public GlobalSearchViewModel GlobalSearch { get; }
    public CommandPaletteViewModel CommandPalette { get; }

    public MainWindowViewModel(
        WorkspaceState state,
        KanBanViewModel kanBanViewModel,
        TasksViewModel tasksViewModel,
        NotesViewModel notesViewModel,
        IAuthService? authService,
        ICloudKanTabRepository? cloudRepository,
        ISyncService? syncService)
        : this(state, kanBanViewModel, tasksViewModel, notesViewModel, null, authService, cloudRepository, syncService)
    {
    }

    public MainWindowViewModel(
        WorkspaceState state,
        KanBanViewModel kanBanViewModel,
        TasksViewModel tasksViewModel,
        NotesViewModel notesViewModel,
        GlobalSearchViewModel? globalSearch,
        IAuthService? authService,
        ICloudKanTabRepository? cloudRepository,
        ISyncService? syncService)
    {
        _state = state;
        _kanBanViewModel = kanBanViewModel;
        _tasksViewModel = tasksViewModel;
        _notesViewModel = notesViewModel;
        _authService = authService;
        _cloudRepository = cloudRepository;
        _syncService = syncService;

        GlobalSearch = globalSearch ?? new GlobalSearchViewModel(
            _state,
            openTask: OpenTaskFromSearch,
            openNote: OpenNoteFromSearch,
            openBoard: OpenBoardFromSearch,
            openTag: OpenTagFromSearch);

        // Construct palette without self-capture; rebuild with full commands after nav is ready.
        CommandPalette = new CommandPaletteViewModel();
        BuildNavItems();
        SelectedNavItem = NavItems[0];
        CommandPalette.SetCommands(BuildPaletteCommands());
    }

    private void BuildNavItems()
    {
        NavItems.Add(new NavItem { Label = "KanBan", Command = NavigateKanBanCommand });
        NavItems.Add(new NavItem { Label = "Tasks", Command = NavigateTasksCommand });
        NavItems.Add(new NavItem { Label = "Notes", Command = NavigateNotesCommand });
        NavItems.Add(new NavItem { Label = "Settings", Command = NavigateSettingsCommand });
    }

    partial void OnSelectedNavItemChanged(NavItem? value)
    {
        if (value != null)
            value.Command.Execute(null);
    }

    /// <summary>Persists all pending changes (called when the app closes).</summary>
    public void FlushPendingSave() => _state.FlushPendingSave();

    [RelayCommand]
    private void NavigateKanBan() => CurrentSection = _kanBanViewModel;

    [RelayCommand]
    private void NavigateTasks() => CurrentSection = _tasksViewModel;

    [RelayCommand]
    private void NavigateNotes() => CurrentSection = _notesViewModel;

    private Services.IFileDialogService? _fileDialogService;

    public void SetFileDialogService(Services.IFileDialogService service) => _fileDialogService = service;

    [RelayCommand]
    private void NavigateSettings()
    {
        // Keep auth wiring when configured; otherwise still pass workspace+dialog for backup actions.
        if (_authService != null && _cloudRepository != null && _syncService != null)
            CurrentSection = new SettingsViewModel(_authService, _cloudRepository, _syncService, _state, _fileDialogService);
        else
            CurrentSection = new SettingsViewModel(_state, _fileDialogService);
    }

    private void OpenTaskFromSearch(TaskItem task)
    {
        var targetBoard = _state.Boards.FirstOrDefault(b => b.Id == task.BoardId);
        if (targetBoard == null)
        {
            foreach (var b in _state.Boards)
            {
                foreach (var c in b.Columns)
                {
                    if (c.Tasks.Any(t => t.Id == task.Id))
                    {
                        targetBoard = b;
                        break;
                    }
                }
                if (targetBoard != null) break;
            }
        }
        if (targetBoard != null)
            _state.SelectedBoard = targetBoard;
        NavigateKanBan();
        try { _kanBanViewModel.EditTaskCommand.Execute(task); } catch { }
    }

    private void OpenNoteFromSearch(Note note)
    {
        _notesViewModel.SelectedNote = note;
        NavigateNotes();
    }

    private void OpenBoardFromSearch(Board board)
    {
        _state.SelectedBoard = board;
        NavigateKanBan();
    }

    private void OpenTagFromSearch(string tag)
    {
        _tasksViewModel.SearchText = tag;
        NavigateTasks();
    }

    private IEnumerable<PaletteCommand> BuildPaletteCommands()
    {
        // Reuse existing navigation/creation/search commands — no duplication.
        return new[]
        {
            new PaletteCommand { Id = "new-task", Title = "New Task", Hint = "Create a new task", Execute = () => _kanBanViewModel.AddTaskCommand.Execute(null) },
            new PaletteCommand { Id = "new-note", Title = "New Note", Hint = "Create a new note", Execute = () => _notesViewModel.NewNoteCommand.Execute(null) },
            new PaletteCommand { Id = "open-kanban", Title = "Open KanBan", Execute = () => NavigateKanBan() },
            new PaletteCommand { Id = "open-tasks", Title = "Open Tasks", Execute = () => NavigateTasks() },
            new PaletteCommand { Id = "open-notes", Title = "Open Notes", Execute = () => NavigateNotes() },
            new PaletteCommand { Id = "open-settings", Title = "Open Settings", Execute = () => NavigateSettings() },
            new PaletteCommand { Id = "focus-search", Title = "Focus Global Search", Hint = "Ctrl+K", Execute = () => GlobalSearch.FocusSearchCommand.Execute(null) },
            new PaletteCommand { Id = "toggle-completed", Title = "Toggle Completed Tasks", Hint = "Show / hide completed", Execute = ToggleCompletedTasks },
            new PaletteCommand { Id = "close", Title = "Close / Cancel", Hint = "Esc", Execute = () => CommandPalette.CloseCommand.Execute(null) },
        };
    }

    private void ToggleCompletedTasks()
    {
        // Tasks workspace filter cycles Active <-> All so completed visibility toggles.
        // If user is not on Tasks, navigate there first.
        var wasOnTasks = CurrentSection == _tasksViewModel;
        if (!wasOnTasks) NavigateTasks();
        _tasksViewModel.SelectedFilter = _tasksViewModel.SelectedFilter == "Completed" ? "All"
            : _tasksViewModel.SelectedFilter == "Active" ? "Completed"
            : "Active";
    }
}