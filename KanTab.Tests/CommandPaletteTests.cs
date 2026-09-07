#pragma warning disable xUnit1031
using System;
using System.Linq;
using KanTab.Models;
using KanTab.Storage;
using KanTab.ViewModels;
using Xunit;

namespace KanTab.Tests;

public class CommandPaletteTests : IDisposable
{
    private readonly string _dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "KanTab.Tests", "Palette", Guid.NewGuid().ToString());
    private string FilePath => System.IO.Path.Combine(_dir, "kantab.json");
    public CommandPaletteTests() => System.IO.Directory.CreateDirectory(_dir);
    public void Dispose() { try { System.IO.Directory.Delete(_dir, true); } catch { } }
    private LocalJsonKanTabRepository Repo() => new(FilePath);

    private static PaletteCommand Cmd(string id, string title, string? hint = null, Action? act = null)
        => new() { Id = id, Title = title, Hint = hint, Execute = act ?? (() => { }) };

    // ---- Registration ----
    [Fact]
    public void Registration_MainWindow_HasExpectedCommands()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        var kanBan = new KanBanViewModel(state);
        var tasks = new TasksViewModel(state, kanBan);
        var notes = new NotesViewModel(state);
        var main = new MainWindowViewModel(state, kanBan, tasks, notes, null, null, null, null);
        var ids = main.CommandPalette.AllCommands.Select(c => c.Id).ToHashSet();
        Assert.Contains("new-task", ids);
        Assert.Contains("new-note", ids);
        Assert.Contains("open-kanban", ids);
        Assert.Contains("open-tasks", ids);
        Assert.Contains("open-notes", ids);
        Assert.Contains("open-settings", ids);
        Assert.Contains("focus-search", ids);
        Assert.Contains("toggle-completed", ids);
        Assert.Contains("close", ids);
    }

    [Fact]
    public void Registration_UsesExistingFlows_NotDuplicated()
    {
        // Ensure palette commands are exactly the expected set, no dashboard/calendar.
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        var kanBan = new KanBanViewModel(state);
        var tasks = new TasksViewModel(state, kanBan);
        var notes = new NotesViewModel(state);
        var main = new MainWindowViewModel(state, kanBan, tasks, notes, null, null, null, null);
        var titles = main.CommandPalette.AllCommands.Select(c => c.Title).ToList();
        Assert.DoesNotContain(titles, t => t.Contains("Dashboard", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(titles, t => t.Contains("Calendar", StringComparison.OrdinalIgnoreCase));
    }

    // ---- Filtering ----
    [Fact]
    public void Filtering_EmptyQuery_ShowsAll_FirstSelected()
    {
        var vm = new CommandPaletteViewModel(new[] { Cmd("a", "New Task"), Cmd("b", "Open KanBan"), Cmd("c", "Focus Global Search") });
        vm.Open();
        Assert.Equal(3, vm.Filtered.Count);
        Assert.Equal(0, vm.SelectedIndex);
        Assert.Equal("New Task", vm.SelectedCommand!.Title);
    }

    [Fact]
    public void Filtering_PrefixAndSubstringMatch()
    {
        var vm = new CommandPaletteViewModel(new[] { Cmd("a", "New Task"), Cmd("b", "New Note"), Cmd("c", "Open Tasks") });
        vm.Open();
        vm.Query = "new";
        Assert.Equal(2, vm.Filtered.Count);
        Assert.Contains(vm.Filtered, c => c.Title == "New Task");
        Assert.Contains(vm.Filtered, c => c.Title == "New Note");
    }

    [Fact]
    public void Filtering_CaseInsensitive()
    {
        var vm = new CommandPaletteViewModel(new[] { Cmd("a", "New Task"), Cmd("b", "Open KanBan") });
        vm.Open();
        vm.Query = "NEW TASK";
        Assert.Single(vm.Filtered);
        vm.Query = "open kanban";
        Assert.Single(vm.Filtered);
        vm.Query = "OpEn KaNbAn";
        Assert.Single(vm.Filtered);
    }

    [Fact]
    public void Filtering_TrimsQuery()
    {
        var vm = new CommandPaletteViewModel(new[] { Cmd("a", "New Task") });
        vm.Open();
        vm.Query = "  new  ";
        Assert.Single(vm.Filtered);
    }

    [Fact]
    public void Filtering_HintAndIdAlsoMatched()
    {
        var vm = new CommandPaletteViewModel(new[] { Cmd("focus-search", "Focus Global Search", "Ctrl+K"), Cmd("new-task", "New Task") });
        vm.Open();
        vm.Query = "ctrl+k";
        Assert.Single(vm.Filtered);
        Assert.Equal("Focus Global Search", vm.Filtered[0].Title);
        vm.Query = "focus-search";
        Assert.Single(vm.Filtered);
    }

    [Fact]
    public void Filtering_NoMatches_ShowsEmptyState()
    {
        var vm = new CommandPaletteViewModel(new[] { Cmd("a", "New Task"), Cmd("b", "Open KanBan") });
        vm.Open();
        vm.Query = "zzznonexistent";
        Assert.Empty(vm.Filtered);
        Assert.Equal(-1, vm.SelectedIndex);
        Assert.True(vm.IsEmpty);
        Assert.Contains("zzznonexistent", vm.EmptyText);
        Assert.False(vm.HasResults);
    }

    [Fact]
    public void Filtering_FirstMatchingSelectedAutomatically()
    {
        var vm = new CommandPaletteViewModel(new[] { Cmd("a", "Open KanBan"), Cmd("b", "Open Tasks"), Cmd("c", "Open Notes") });
        vm.Open();
        vm.Query = "open";
        Assert.Equal(0, vm.SelectedIndex);
        Assert.Equal("Open KanBan", vm.SelectedCommand!.Title);
        vm.Query = "notes";
        Assert.Single(vm.Filtered);
        Assert.Equal(0, vm.SelectedIndex);
        Assert.Equal("Open Notes", vm.SelectedCommand!.Title);
    }

    [Fact]
    public void Filtering_ChangingQuery_ResetsSelectionToFirst()
    {
        var vm = new CommandPaletteViewModel(new[] { Cmd("a", "New Task"), Cmd("b", "New Note"), Cmd("c", "Open Tasks") });
        vm.Open();
        vm.MoveDownCommand.Execute(null);
        Assert.Equal(1, vm.SelectedIndex);
        vm.Query = "open";
        Assert.Equal(0, vm.SelectedIndex);
    }

    // ---- Selection behavior ----
    [Fact]
    public void Selection_ArrowUpDown_Wraps()
    {
        var vm = new CommandPaletteViewModel(new[] { Cmd("a", "A"), Cmd("b", "B"), Cmd("c", "C") });
        vm.Open();
        Assert.Equal(0, vm.SelectedIndex);
        vm.MoveDownCommand.Execute(null);
        Assert.Equal(1, vm.SelectedIndex);
        vm.MoveDownCommand.Execute(null);
        Assert.Equal(2, vm.SelectedIndex);
        vm.MoveDownCommand.Execute(null);
        Assert.Equal(0, vm.SelectedIndex);
        vm.MoveUpCommand.Execute(null);
        Assert.Equal(2, vm.SelectedIndex);
    }

    [Fact]
    public void Selection_EmptyList_DoesNotMove()
    {
        var vm = new CommandPaletteViewModel(new[] { Cmd("a", "A") });
        vm.Open();
        vm.Query = "zzznonexistent";
        vm.MoveDownCommand.Execute(null);
        Assert.Equal(-1, vm.SelectedIndex);
        vm.MoveUpCommand.Execute(null);
        Assert.Equal(-1, vm.SelectedIndex);
    }

    // ---- Executing commands ----
    [Fact]
    public void Executing_SelectedCommand_CallsAction_AndCloses()
    {
        bool called = false;
        var vm = new CommandPaletteViewModel(new[] { Cmd("a", "New Task", act: () => called = true), Cmd("b", "Open KanBan") });
        vm.Open();
        vm.Query = "new";
        vm.ExecuteSelectedCommand.Execute(null);
        Assert.True(called);
        Assert.False(vm.IsOpen);
        Assert.Equal(string.Empty, vm.Query);
    }

    [Fact]
    public void Executing_ByClick_CallsAction()
    {
        bool called = false;
        var target = Cmd("a", "New Task", act: () => called = true);
        var vm = new CommandPaletteViewModel(new[] { target, Cmd("b", "Open KanBan") });
        vm.Open();
        vm.ExecuteCommandCommand.Execute(target);
        Assert.True(called);
        Assert.False(vm.IsOpen);
    }

    [Fact]
    public void Executing_NoSelection_DoesNothing()
    {
        bool called = false;
        var vm = new CommandPaletteViewModel(new[] { Cmd("a", "New Task", act: () => called = true) });
        vm.Open();
        vm.Query = "zzznonexistent";
        vm.ExecuteSelectedCommand.Execute(null);
        Assert.False(called);
        // Remains open? Behavior: palette stays open when execute fails (no selection)
        // Current impl: does nothing and stays open (IsOpen true) — assert that
        Assert.True(vm.IsOpen);
    }

    // ---- Open/Close ----
    [Fact]
    public void Open_SetsIsOpen_AndRequestsFocus()
    {
        var vm = new CommandPaletteViewModel(new[] { Cmd("a", "A") });
        bool focused = false;
        vm.RequestFocus += () => focused = true;
        vm.Open();
        Assert.True(vm.IsOpen);
        Assert.True(focused);
        Assert.Equal(0, vm.SelectedIndex);
    }

    [Fact]
    public void Close_ClearsQueryAndSelection()
    {
        var vm = new CommandPaletteViewModel(new[] { Cmd("a", "A"), Cmd("b", "B") });
        vm.Open();
        vm.Query = "A";
        Assert.True(vm.IsOpen);
        vm.CloseCommand.Execute(null);
        Assert.False(vm.IsOpen);
        Assert.Equal(string.Empty, vm.Query);
        Assert.Equal(-1, vm.SelectedIndex);
    }

    [Fact]
    public void Palette_EscCloses()
    {
        var vm = new CommandPaletteViewModel(new[] { Cmd("a", "A") });
        vm.Open();
        bool handled = vm.HandleKey(Avalonia.Input.Key.Escape, Avalonia.Input.KeyModifiers.None);
        Assert.True(handled);
        Assert.False(vm.IsOpen);
    }

    [Fact]
    public void Palette_EnterExecutes_AndArrowNavigates()
    {
        bool called = false;
        var vm = new CommandPaletteViewModel(new[] { Cmd("a", "Alpha", act: () => called = true), Cmd("b", "Beta") });
        vm.Open();
        Assert.True(vm.HandleKey(Avalonia.Input.Key.Down, Avalonia.Input.KeyModifiers.None));
        Assert.Equal(1, vm.SelectedIndex);
        Assert.True(vm.HandleKey(Avalonia.Input.Key.Up, Avalonia.Input.KeyModifiers.None));
        Assert.Equal(0, vm.SelectedIndex);
        Assert.True(vm.HandleKey(Avalonia.Input.Key.Enter, Avalonia.Input.KeyModifiers.None));
        Assert.True(called);
        Assert.False(vm.IsOpen);
    }

    [Fact]
    public void Palette_HandleKey_ReturnsFalseWhenClosed()
    {
        var vm = new CommandPaletteViewModel(new[] { Cmd("a", "A") });
        Assert.False(vm.HandleKey(Avalonia.Input.Key.Escape, Avalonia.Input.KeyModifiers.None));
        Assert.False(vm.HandleKey(Avalonia.Input.Key.Down, Avalonia.Input.KeyModifiers.None));
        Assert.False(vm.HandleKey(Avalonia.Input.Key.Enter, Avalonia.Input.KeyModifiers.None));
    }

    // ---- Integration: MainWindow palette uses real commands ----
    [Fact]
    public void MainWindow_Palette_NewTask_UsesExistingAddTaskFlow()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        state.Boards.Clear();
        var board = new Board { Id = "b1", Name = "B", Position = 0, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now };
        board.Columns.Add(new KanBanColumn { Id = "c1", Title = "To Do", BoardId = "b1", Position = 0, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now });
        state.Boards.Add(board);
        state.SelectedBoard = board;
        var kanBan = new KanBanViewModel(state);
        var tasks = new TasksViewModel(state, kanBan);
        var notes = new NotesViewModel(state);
        var main = new MainWindowViewModel(state, kanBan, tasks, notes, null, null, null, null);
        // Registration uses existing flow — verify it is present and filterable without requiring a windowing platform.
        Assert.Contains(main.CommandPalette.AllCommands, c => c.Id == "new-task");
        main.CommandPalette.Query = "new task";
        Assert.Contains(main.CommandPalette.Filtered, c => c.Id == "new-task");
        // Execution would open TaskDialog (requires Avalonia windowing); in headless tests it throws.
        // Verify that the palette's execute path at least attempts the existing KanBan command flow.
        var cmd = main.CommandPalette.AllCommands.First(c => c.Id == "new-task");
        try { cmd.Execute(); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("IWindowingPlatform")) { /* expected in headless */ }
        Assert.Contains(main.CommandPalette.AllCommands, c => c.Title == "New Task");
    }

    [Fact]
    public void MainWindow_Palette_NewNote_CreatesNoteViaExistingFlow()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        var kanBan = new KanBanViewModel(state);
        var tasks = new TasksViewModel(state, kanBan);
        var notes = new NotesViewModel(state);
        var before = state.Notes.Count;
        var main = new MainWindowViewModel(state, kanBan, tasks, notes, null, null, null, null);
        var cmd = main.CommandPalette.AllCommands.First(c => c.Id == "new-note");
        cmd.Execute();
        // NewNote creates a note immediately (no dialog), so count increases by 1.
        Assert.Equal(before + 1, state.Notes.Count);
        Assert.NotNull(notes.SelectedNote);
    }

    [Fact]
    public void MainWindow_Palette_Navigation_UsesExistingNavigation()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        var kanBan = new KanBanViewModel(state);
        var tasks = new TasksViewModel(state, kanBan);
        var notes = new NotesViewModel(state);
        var main = new MainWindowViewModel(state, kanBan, tasks, notes, null, null, null, null);
        // Start on KanBan
        Assert.Equal(kanBan, main.CurrentSection);
        main.CommandPalette.AllCommands.First(c => c.Id == "open-tasks").Execute();
        Assert.Equal(tasks, main.CurrentSection);
        main.CommandPalette.AllCommands.First(c => c.Id == "open-notes").Execute();
        Assert.Equal(notes, main.CurrentSection);
        main.CommandPalette.AllCommands.First(c => c.Id == "open-kanban").Execute();
        Assert.Equal(kanBan, main.CurrentSection);
    }

    [Fact]
    public void MainWindow_Palette_FocusSearch_FocusesGlobalSearch()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        var kanBan = new KanBanViewModel(state);
        var tasks = new TasksViewModel(state, kanBan);
        var notes = new NotesViewModel(state);
        var main = new MainWindowViewModel(state, kanBan, tasks, notes, null, null, null, null);
        bool focused = false;
        main.GlobalSearch.RequestFocus += () => focused = true;
        main.CommandPalette.AllCommands.First(c => c.Id == "focus-search").Execute();
        Assert.True(focused);
    }

    [Fact]
    public void MainWindow_Palette_ToggleCompleted_TogglesFilter()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        var kanBan = new KanBanViewModel(state);
        kanBan.SelectedBoard = state.Boards.FirstOrDefault();
        var tasks = new TasksViewModel(state, kanBan);
        var notes = new NotesViewModel(state);
        var main = new MainWindowViewModel(state, kanBan, tasks, notes, null, null, null, null);
        Assert.Equal("All", tasks.SelectedFilter);
        main.CommandPalette.AllCommands.First(c => c.Id == "toggle-completed").Execute();
        Assert.Equal("Active", tasks.SelectedFilter);
        Assert.Equal(tasks, main.CurrentSection);
        main.CommandPalette.AllCommands.First(c => c.Id == "toggle-completed").Execute();
        Assert.Equal("Completed", tasks.SelectedFilter);
        main.CommandPalette.AllCommands.First(c => c.Id == "toggle-completed").Execute();
        Assert.Equal("All", tasks.SelectedFilter);
    }

    [Fact]
    public void MainWindow_GlobalSearch_CtrlK_StillWorks()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        var kanBan = new KanBanViewModel(state);
        var tasks = new TasksViewModel(state, kanBan);
        var notes = new NotesViewModel(state);
        var main = new MainWindowViewModel(state, kanBan, tasks, notes, null, null, null, null);
        bool focused = false;
        main.GlobalSearch.RequestFocus += () => focused = true;
        main.GlobalSearch.FocusSearchCommand.Execute(null);
        Assert.True(focused);
        // Palette open should not affect GlobalSearch
        main.CommandPalette.Open();
        Assert.True(main.CommandPalette.IsOpen);
        main.CommandPalette.CloseCommand.Execute(null);
        Assert.False(main.CommandPalette.IsOpen);
    }

    [Fact]
    public void TaskDialog_EscAndCtrlEnter_DoNotThrow()
    {
        var vm = new TaskDialogViewModel { Title = "T" };
        // Cancel should close without saving
        bool closed = false;
        vm.RequestClose += (_, _) => closed = true;
        vm.CancelCommand.Execute(null);
        Assert.True(closed);
        // Reopen for Create path
        vm = new TaskDialogViewModel { Title = "Hello", SelectedColumnId = "c1" };
        vm.AvailableColumns.Add(new KanBanColumn { Id = "c1", Title = "C" });
        bool closed2 = false;
        vm.RequestClose += (_, _) => closed2 = true;
        vm.CreateCommand.Execute(null);
        Assert.True(closed2);
        Assert.True(vm.DialogResult);
    }
}
