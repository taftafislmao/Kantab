using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using KanTab.Models;
using KanTab.Storage;

namespace KanTab.ViewModels;

/// <summary>
/// The single in-memory source of truth shared by the KanBan, Tasks, and Notes
/// workspaces. Backed by an <see cref="IKanTabRepository"/>; mutations mark the
/// state dirty and a debounced save (or an explicit flush on close) persists it.
/// </summary>
public partial class WorkspaceState : ObservableObject
{
    private readonly IKanTabRepository _repository;
    private readonly object _saveGate = new();
    private readonly DispatcherTimer? _saveTimer;

    public ObservableCollection<Board> Boards { get; } = new();
    public ObservableCollection<Note> Notes { get; } = new();

    /// <summary>Raised after any board/task mutation so projections can refresh.</summary>
    public event EventHandler? Changed;

    [ObservableProperty]
    private Board? _selectedBoard;

    [ObservableProperty]
    private Note? _selectedNote;

    [ObservableProperty]
    private string? _storageError;

    /// <summary>Non-fatal problem reported by the repository when loading.</summary>
    public string? LoadError { get; private set; }

    public bool HasUnsavedChanges { get; private set; }

    private WorkspaceState(IKanTabRepository repository, bool useDispatcherTimer)
    {
        _repository = repository;
        if (useDispatcherTimer)
        {
            _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            _saveTimer.Tick += (_, _) =>
            {
                _saveTimer.Stop();
                SaveNow();
            };
        }
    }

    /// <summary>
    /// Loads persisted data from the repository. Sample data is seeded only on
    /// the very first launch (nothing saved yet, nothing to lose); a corrupt
    /// storage file never triggers seeding so the original file is preserved.
    /// </summary>
    public static WorkspaceState Load(IKanTabRepository repository, bool useDispatcherTimer = true)
    {
        var state = new WorkspaceState(repository, useDispatcherTimer);
        var data = repository.LoadAsync().GetAwaiter().GetResult();

        state.LoadError = (repository as LocalJsonKanTabRepository)?.LastLoadError;

        if (state.LoadError == null && SampleDataSeeder.IsEmpty(data))
        {
            // First launch: seed sample content and persist it immediately so
            // it is never recreated once real user data exists.
            foreach (var board in SampleDataSeeder.CreateDefaultBoards())
                data.Boards.Add(board);
            data = LocalJsonKanTabRepository.Sanitize(data);
            try
            {
                repository.SaveAsync(data).GetAwaiter().GetResult();
            }
            catch
            {
                // Seeded data stays in memory; the next save will retry.
                state.HasUnsavedChanges = true;
            }
        }

        foreach (var board in data.Boards.OrderBy(b => b.Position))
            state.Boards.Add(board);
        foreach (var note in data.Notes)
            state.Notes.Add(note);

        if (data.Settings != null)
        {
            state.Settings.LastLaunchedVersion = data.Settings.LastLaunchedVersion;
            state.Settings.NotificationsEnabled = data.Settings.NotificationsEnabled;
            state.Settings.ReminderLeadMinutes = data.Settings.ReminderLeadMinutes;
            state.Settings.OverdueNotificationsEnabled = data.Settings.OverdueNotificationsEnabled;
            state.Settings.InitialSetupCompleted = data.Settings.InitialSetupCompleted;
        }

        state.SelectedBoard = state.Boards.FirstOrDefault();

        if (state.LoadError != null)
            state.StorageError = "Stored data could not be read. A new empty workspace is being used; the original file was not modified.";

        return state;
    }

    /// <summary>
    /// Call after any board, column, task, or note mutation: refreshes
    /// projections and schedules a debounced save.
    /// </summary>
    public void NotifyChanged()
    {
        MarkDirty();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Schedules a debounced save without raising the change event.</summary>
    public void MarkDirty()
    {
        HasUnsavedChanges = true;
        if (_saveTimer == null)
        {
            // No dispatcher available (tests / headless use): save immediately.
            SaveNow();
        }
        else
        {
            _saveTimer.Stop();
            _saveTimer.Start();
        }
    }

    /// <summary>Writes the current state to the repository immediately. Thread-safe.</summary>
    public bool SaveNow()
    {
        lock (_saveGate)
        {
            _saveTimer?.Stop();
            try
            {
                _repository.SaveAsync(BuildSnapshot()).GetAwaiter().GetResult();
                HasUnsavedChanges = false;
                StorageError = null;
                return true;
            }
            catch (Exception ex)
            {
                StorageError = $"Changes are kept in memory, but saving failed: {ex.Message}";
                return false;
            }
        }
    }

    /// <summary>Flushes any pending debounced save; called when the app closes.</summary>
    public bool FlushPendingSave()
    {
        lock (_saveGate)
        {
            if (!HasUnsavedChanges)
                return true;
        }

        return SaveNow();
    }

    /// <summary>App settings that are persisted via BuildSnapshot / SaveNow.</summary>
    public AppSettings Settings { get; } = new();

    public KanTabData BuildSnapshot() => new()
    {
        Boards = Boards.OrderBy(b => b.Position).ToList(),
        Notes = Notes.ToList(),
        Settings = new AppSettings
        {
            LastLaunchedVersion = Settings.LastLaunchedVersion ?? "1.0.0",
            NotificationsEnabled = Settings.NotificationsEnabled,
            ReminderLeadMinutes = Settings.ReminderLeadMinutes,
            OverdueNotificationsEnabled = Settings.OverdueNotificationsEnabled,
            InitialSetupCompleted = Settings.InitialSetupCompleted
        }
    };
}
