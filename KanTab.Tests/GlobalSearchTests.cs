using KanTab.Models;
using KanTab.Services;
using KanTab.Storage;
using KanTab.ViewModels;
using Xunit;

namespace KanTab.Tests;

public class GlobalSearchServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "KanTab.Tests", "GlobalSearch", Guid.NewGuid().ToString());
    private string FilePath => Path.Combine(_directory, "kantab.json");
    public GlobalSearchServiceTests() => Directory.CreateDirectory(_directory);
    public void Dispose() { try { Directory.Delete(_directory, true); } catch { } }
    private LocalJsonKanTabRepository Repo() => new(FilePath);

    private WorkspaceState CreateStateWithData()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        state.Boards.Clear();
        state.Notes.Clear();

        var now = DateTime.Now;
        var board1 = new Board { Id = "b1", Name = "Alpha Board", Position = 0, CreatedAt = now, UpdatedAt = now };
        var col1 = new KanBanColumn { Id = "c1", Title = "To Do", BoardId = board1.Id, Position = 0, CreatedAt = now, UpdatedAt = now };
        var col2 = new KanBanColumn { Id = "c2", Title = "In Progress", BoardId = board1.Id, Position = 1, CreatedAt = now, UpdatedAt = now };
        col1.Tasks.Add(new TaskItem { Id = "t1", Title = "Fix login bug", Description = "User cannot login with valid credentials", Priority = Priority.High, ColumnId = col1.Id, BoardId = board1.Id, Position = 0, CreatedAt = now.AddMinutes(-10), UpdatedAt = now.AddMinutes(-10), Tags = new() { "bug", "backend" } });
        col1.Tasks.Add(new TaskItem { Id = "t2", Title = "Design homepage", Description = "Create new design for homepage", Priority = Priority.Medium, ColumnId = col1.Id, BoardId = board1.Id, Position = 1, CreatedAt = now.AddMinutes(-5), UpdatedAt = now.AddMinutes(-5), Tags = new() { "frontend", "design" } });
        col2.Tasks.Add(new TaskItem { Id = "t3", Title = "Write docs", Description = "Document the API endpoints", Priority = Priority.Low, ColumnId = col2.Id, BoardId = board1.Id, Position = 0, CreatedAt = now.AddMinutes(-2), UpdatedAt = now.AddMinutes(-2), Tags = new() { "Docs" } });
        board1.Columns.Add(col1);
        board1.Columns.Add(col2);

        var board2 = new Board { Id = "b2", Name = "Beta Board", Position = 1, CreatedAt = now, UpdatedAt = now };
        var col3 = new KanBanColumn { Id = "c3", Title = "Done", BoardId = board2.Id, Position = 0, CreatedAt = now, UpdatedAt = now };
        col3.Tasks.Add(new TaskItem { Id = "t4", Title = "Setup CI", Description = "Configure CI pipeline", Priority = Priority.Medium, ColumnId = col3.Id, BoardId = board2.Id, Position = 0, CreatedAt = now.AddMinutes(-1), UpdatedAt = now.AddMinutes(-1), Tags = new() { "setup" } });
        board2.Columns.Add(col3);

        state.Boards.Add(board1);
        state.Boards.Add(board2);

        state.Notes.Add(new Note { Id = "n1", Title = "Shopping list", Content = "Buy milk and eggs", IsPinned = false, CreatedAt = now.AddMinutes(-8), UpdatedAt = now.AddMinutes(-8) });
        state.Notes.Add(new Note { Id = "n2", Title = "Meeting notes", Content = "Discuss project roadmap and budget", IsPinned = true, CreatedAt = now.AddMinutes(-3), UpdatedAt = now.AddMinutes(-3) });
        state.Notes.Add(new Note { Id = "n3", Title = "Ideas", Content = "New feature for frontend dashboard", IsPinned = false, CreatedAt = now.AddMinutes(-6), UpdatedAt = now.AddMinutes(-6) });

        state.SelectedBoard = board1;
        return state;
    }

    [Fact]
    public void TaskTitleSearch_FindsMatchingTask()
    {
        var state = CreateStateWithData();
        var result = GlobalSearchService.Search(state, "login");
        Assert.Single(result.TaskHits);
        Assert.Equal("Fix login bug", result.TaskHits[0].Task.Title);
    }

    [Fact]
    public void TaskDescriptionSearch_FindsMatchingTask()
    {
        var state = CreateStateWithData();
        var result = GlobalSearchService.Search(state, "credentials");
        Assert.Single(result.TaskHits);
        Assert.Equal("Fix login bug", result.TaskHits[0].Task.Title);
    }

    [Fact]
    public void TaskTagSearch_FindsMatchingTask()
    {
        var state = CreateStateWithData();
        var result = GlobalSearchService.Search(state, "frontend");
        Assert.Contains(result.TaskHits, h => h.Task.Title == "Design homepage");
    }

    [Fact]
    public void TaskTagSearch_WithHash_FindsMatchingTask()
    {
        var state = CreateStateWithData();
        var result = GlobalSearchService.Search(state, "#backend");
        Assert.Contains(result.TaskHits, h => h.Task.Tags.Contains("backend"));
    }

    [Fact]
    public void NoteTitleSearch_FindsMatchingNote()
    {
        var state = CreateStateWithData();
        var result = GlobalSearchService.Search(state, "Shopping");
        Assert.Single(result.NoteHits);
        Assert.Equal("Shopping list", result.NoteHits[0].Note.Title);
    }

    [Fact]
    public void NoteContentSearch_FindsMatchingNote()
    {
        var state = CreateStateWithData();
        var result = GlobalSearchService.Search(state, "milk");
        Assert.Single(result.NoteHits);
        Assert.Equal("Shopping list", result.NoteHits[0].Note.Title);
    }

    [Fact]
    public void BoardSearch_FindsMatchingBoard()
    {
        var state = CreateStateWithData();
        var result = GlobalSearchService.Search(state, "Alpha");
        Assert.Single(result.BoardHits);
        Assert.Equal("Alpha Board", result.BoardHits[0].Board.Name);
    }

    [Fact]
    public void CaseInsensitiveSearch_MatchesRegardlessOfCase()
    {
        var state = CreateStateWithData();
        var lower = GlobalSearchService.Search(state, "alpha");
        var upper = GlobalSearchService.Search(state, "ALPHA");
        var mixed = GlobalSearchService.Search(state, "AlPhA");
        Assert.Single(lower.BoardHits);
        Assert.Single(upper.BoardHits);
        Assert.Single(mixed.BoardHits);
        Assert.Equal("Alpha Board", lower.BoardHits[0].Board.Name);
    }

    [Fact]
    public void WhitespaceHandling_TrimsQuery()
    {
        var state = CreateStateWithData();
        var result = GlobalSearchService.Search(state, "  login  ");
        Assert.Single(result.TaskHits);
    }

    [Fact]
    public void EmptyQuery_ReturnsNoResults()
    {
        var state = CreateStateWithData();
        var result = GlobalSearchService.Search(state, "");
        Assert.Empty(result.TaskHits);
        Assert.Empty(result.NoteHits);
        Assert.Empty(result.BoardHits);
        Assert.Empty(result.TagHits);
        Assert.False(result.HasAnyResults);
    }

    [Fact]
    public void WhitespaceOnlyQuery_ReturnsNoResults()
    {
        var state = CreateStateWithData();
        var result = GlobalSearchService.Search(state, "   ");
        Assert.False(result.HasAnyResults);
    }

    [Fact]
    public void NoResults_ReturnsEmptyGroups()
    {
        var state = CreateStateWithData();
        var result = GlobalSearchService.Search(state, "zzzzzznonexistent");
        Assert.Empty(result.TaskHits);
        Assert.Empty(result.NoteHits);
        Assert.Empty(result.BoardHits);
        Assert.Empty(result.TagHits);
    }

    [Fact]
    public void ResultsGroupedCorrectly_OnlyGroupsWithResults()
    {
        var state = CreateStateWithData();
        var result = GlobalSearchService.Search(state, "Alpha");
        Assert.NotEmpty(result.BoardHits);
        Assert.Empty(result.TaskHits);
        Assert.Empty(result.NoteHits);
        // Tag hits should be empty for board-only query
        Assert.Empty(result.TagHits);
    }

    [Fact]
    public void ResultsGroupedCorrectly_MultipleGroups()
    {
        var state = CreateStateWithData();
        // "frontend" appears in task tag and note content
        var result = GlobalSearchService.Search(state, "frontend");
        Assert.NotEmpty(result.TaskHits);
        Assert.NotEmpty(result.NoteHits);
        Assert.NotEmpty(result.TagHits);
    }

    [Fact]
    public void ResultOrdering_TasksOrderedByUpdatedAtDescending()
    {
        var state = CreateStateWithData();
        // Add two tasks matching same query with different UpdatedAt
        var now = DateTime.Now;
        var board = state.Boards[0];
        var col = board.Columns[0];
        var older = new TaskItem { Id = "tOld", Title = "Common keyword task old", Description = "", ColumnId = col.Id, BoardId = board.Id, Position = 99, CreatedAt = now.AddHours(-2), UpdatedAt = now.AddHours(-2), Tags = new() };
        var newer = new TaskItem { Id = "tNew", Title = "Common keyword task new", Description = "", ColumnId = col.Id, BoardId = board.Id, Position = 100, CreatedAt = now, UpdatedAt = now, Tags = new() };
        col.Tasks.Add(older);
        col.Tasks.Add(newer);
        var result = GlobalSearchService.Search(state, "Common keyword");
        Assert.Equal(2, result.TaskHits.Count);
        Assert.Equal("tNew", result.TaskHits[0].Task.Id);
        Assert.Equal("tOld", result.TaskHits[1].Task.Id);
    }

    [Fact]
    public void ResultOrdering_NotesPinnedFirst()
    {
        var state = CreateStateWithData();
        state.Notes.Clear();
        var now = DateTime.Now;
        var unpinned = new Note { Id = "nU", Title = "Common note unpinned", Content = "Common note", IsPinned = false, CreatedAt = now, UpdatedAt = now };
        var pinned = new Note { Id = "nP", Title = "Common note pinned", Content = "Common note", IsPinned = true, CreatedAt = now.AddMinutes(-10), UpdatedAt = now.AddMinutes(-10) };
        state.Notes.Add(unpinned);
        state.Notes.Add(pinned);
        var result = GlobalSearchService.Search(state, "Common note");
        Assert.Equal(2, result.NoteHits.Count);
        Assert.True(result.NoteHits[0].Note.IsPinned);
        Assert.Equal("nP", result.NoteHits[0].Note.Id);
    }

    [Fact]
    public void ResultOrdering_BoardsByPosition()
    {
        var state = CreateStateWithData();
        var result = GlobalSearchService.Search(state, "Board");
        Assert.Equal(2, result.BoardHits.Count);
        Assert.Equal("Alpha Board", result.BoardHits[0].Board.Name);
        Assert.Equal("Beta Board", result.BoardHits[1].Board.Name);
    }

    [Fact]
    public void ResultOrdering_TagsAlphabetical()
    {
        var state = CreateStateWithData();
        var now = DateTime.Now;
        var board = state.Boards[0];
        var col = board.Columns[0];
        col.Tasks.Add(new TaskItem { Id = "tZ", Title = "Z task", Description = "", ColumnId = col.Id, BoardId = board.Id, Position = 10, CreatedAt = now, UpdatedAt = now, Tags = new() { "zzzTag" } });
        col.Tasks.Add(new TaskItem { Id = "tA", Title = "A task", Description = "", ColumnId = col.Id, BoardId = board.Id, Position = 11, CreatedAt = now, UpdatedAt = now, Tags = new() { "aaaTag" } });
        var result = GlobalSearchService.Search(state, "Tag");
        var tags = result.TagHits.Select(t => t.Tag).ToList();
        var sorted = tags.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();
        Assert.Equal(sorted, tags);
    }

    [Fact]
    public void TagGroup_ReturnsDistinctMatchingTags()
    {
        var state = CreateStateWithData();
        var result = GlobalSearchService.Search(state, "bug");
        Assert.Contains(result.TagHits, t => t.Tag.Equals("bug", StringComparison.OrdinalIgnoreCase));
        Assert.Single(result.TagHits, t => t.Tag.Equals("bug", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TagSearch_CaseInsensitive()
    {
        var state = CreateStateWithData();
        var result = GlobalSearchService.Search(state, "BUG");
        Assert.Contains(result.TagHits, t => t.Tag.Equals("bug", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Search_UsesInMemoryState_ReflectsUpdatedData()
    {
        var state = CreateStateWithData();
        var before = GlobalSearchService.Search(state, "BrandNewTitle");
        Assert.Empty(before.TaskHits);
        var board = state.Boards[0];
        var col = board.Columns[0];
        col.Tasks.Add(new TaskItem { Id = "tNew", Title = "BrandNewTitle", Description = "", ColumnId = col.Id, BoardId = board.Id, Position = 99, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now, Tags = new() });
        var after = GlobalSearchService.Search(state, "BrandNewTitle");
        Assert.Single(after.TaskHits);
    }

    [Fact]
    public void BoardSearch_IgnoresDeletedBoards()
    {
        var state = CreateStateWithData();
        state.Boards[0].DeletedAt = DateTime.Now;
        var result = GlobalSearchService.Search(state, "Alpha");
        Assert.Empty(result.BoardHits);
    }
}

public class GlobalSearchViewModelTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "KanTab.Tests", "GlobalSearchVM", Guid.NewGuid().ToString());
    private string FilePath => Path.Combine(_directory, "kantab.json");
    public GlobalSearchViewModelTests() => Directory.CreateDirectory(_directory);
    public void Dispose() { try { Directory.Delete(_directory, true); } catch { } }
    private LocalJsonKanTabRepository Repo() => new(FilePath);

    private WorkspaceState CreateState()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        state.Boards.Clear();
        state.Notes.Clear();
        var now = DateTime.Now;
        var board = new Board { Id = "b1", Name = "Alpha Board", Position = 0, CreatedAt = now, UpdatedAt = now };
        var col = new KanBanColumn { Id = "c1", Title = "To Do", BoardId = board.Id, Position = 0, CreatedAt = now, UpdatedAt = now };
        col.Tasks.Add(new TaskItem { Id = "t1", Title = "Alpha task", Description = "desc alpha", ColumnId = col.Id, BoardId = board.Id, Position = 0, CreatedAt = now, UpdatedAt = now, Tags = new() { "alphaTag" } });
        board.Columns.Add(col);
        state.Boards.Add(board);
        state.Notes.Add(new Note { Id = "n1", Title = "Alpha note", Content = "alpha content", IsPinned = false, CreatedAt = now, UpdatedAt = now });
        state.SelectedBoard = board;
        return state;
    }

    [Fact]
    public void ViewModel_EmptyQuery_HasNoResultsAndNotOpen()
    {
        var state = CreateState();
        var vm = new GlobalSearchViewModel(state);
        vm.Query = "";
        Assert.False(vm.HasQuery);
        Assert.True(vm.IsEmptyQuery);
        Assert.False(vm.HasAnyResults);
        Assert.False(vm.IsOpen);
        Assert.False(vm.NoResults);
    }

    [Fact]
    public void ViewModel_WhitespaceQuery_TreatedAsEmpty()
    {
        var state = CreateState();
        var vm = new GlobalSearchViewModel(state);
        vm.Query = "   ";
        Assert.False(vm.HasQuery);
        Assert.False(vm.HasAnyResults);
    }

    [Fact]
    public void ViewModel_ValidQuery_ProducesGroupedResults()
    {
        var state = CreateState();
        var vm = new GlobalSearchViewModel(state);
        vm.Query = "Alpha";
        Assert.True(vm.HasTaskResults);
        Assert.True(vm.HasNoteResults);
        Assert.True(vm.HasBoardResults);
        Assert.True(vm.HasAnyResults);
        Assert.True(vm.IsOpen);
        Assert.Equal(0, vm.SelectedIndex);
        Assert.NotNull(vm.SelectedHit);
    }

    [Fact]
    public void ViewModel_NoResults_ShowsNoResultsText()
    {
        var state = CreateState();
        var vm = new GlobalSearchViewModel(state);
        vm.Query = "nonexistentzzzz";
        Assert.True(vm.NoResults);
        Assert.Contains("nonexistentzzzz", vm.NoResultsText);
        Assert.True(vm.IsOpen);
        Assert.Equal(-1, vm.SelectedIndex);
    }

    [Fact]
    public void ViewModel_KeyboardNavigation_MoveUpDownWraps()
    {
        var state = CreateState();
        var vm = new GlobalSearchViewModel(state);
        vm.Query = "Alpha";
        var total = vm.TotalCount;
        Assert.True(total >= 3);
        var first = vm.SelectedIndex;
        vm.MoveDownCommand.Execute(null);
        Assert.Equal((first + 1) % total, vm.SelectedIndex);
        vm.MoveUpCommand.Execute(null);
        Assert.Equal(first, vm.SelectedIndex);
        // Wrap: up from 0 goes to last
        vm.MoveUpCommand.Execute(null);
        Assert.Equal(total - 1, vm.SelectedIndex);
        vm.MoveDownCommand.Execute(null);
        Assert.Equal(0, vm.SelectedIndex);
    }

    [Fact]
    public void ViewModel_Clear_ResetsQueryAndClose()
    {
        var state = CreateState();
        var vm = new GlobalSearchViewModel(state);
        vm.Query = "Alpha";
        Assert.True(vm.IsOpen);
        vm.ClearCommand.Execute(null);
        Assert.Equal(string.Empty, vm.Query);
        Assert.False(vm.IsOpen);
        Assert.False(vm.HasQuery);
    }

    [Fact]
    public void ViewModel_FlatResults_OrderIsTasksThenNotesThenBoardsThenTags()
    {
        var state = CreateState();
        var vm = new GlobalSearchViewModel(state);
        vm.Query = "Alpha";
        var flat = vm.GetFlatResults();
        Assert.True(flat[0] is TaskHitViewModel);
        Assert.True(flat[1] is NoteHitViewModel);
        Assert.True(flat[2] is BoardHitViewModel);
        Assert.Contains(flat, f => f is TagHitViewModel);
    }

    [Fact]
    public void ViewModel_IsSelected_UpdatesWithSelection()
    {
        var state = CreateState();
        var vm = new GlobalSearchViewModel(state);
        vm.Query = "Alpha";
        var flat = vm.GetFlatResults();
        Assert.True(flat[0].IsSelected);
        vm.MoveDownCommand.Execute(null);
        Assert.False(flat[0].IsSelected);
        Assert.True(flat[1].IsSelected);
    }
}

public class GlobalSearchRoutingTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "KanTab.Tests", "GlobalSearchRouting", Guid.NewGuid().ToString());
    private string FilePath => Path.Combine(_directory, "kantab.json");
    public GlobalSearchRoutingTests() => Directory.CreateDirectory(_directory);
    public void Dispose() { try { Directory.Delete(_directory, true); } catch { } }
    private LocalJsonKanTabRepository Repo() => new(FilePath);

    private WorkspaceState CreateState()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        state.Boards.Clear();
        state.Notes.Clear();
        var now = DateTime.Now;
        var board1 = new Board { Id = "b1", Name = "Board One", Position = 0, CreatedAt = now, UpdatedAt = now };
        var col1 = new KanBanColumn { Id = "c1", Title = "To Do", BoardId = board1.Id, Position = 0, CreatedAt = now, UpdatedAt = now };
        col1.Tasks.Add(new TaskItem { Id = "t1", Title = "Task One", Description = "desc", ColumnId = col1.Id, BoardId = board1.Id, Position = 0, CreatedAt = now, UpdatedAt = now, Tags = new() { "bug" } });
        board1.Columns.Add(col1);
        var board2 = new Board { Id = "b2", Name = "Board Two", Position = 1, CreatedAt = now, UpdatedAt = now };
        var col2 = new KanBanColumn { Id = "c2", Title = "Done", BoardId = board2.Id, Position = 0, CreatedAt = now, UpdatedAt = now };
        board2.Columns.Add(col2);
        state.Boards.Add(board1);
        state.Boards.Add(board2);
        state.Notes.Add(new Note { Id = "n1", Title = "Note One", Content = "content", IsPinned = false, CreatedAt = now, UpdatedAt = now });
        state.SelectedBoard = board1;
        return state;
    }

    [Fact]
    public void SelectingTask_RoutesToCorrectAction()
    {
        var state = CreateState();
        TaskItem? openedTask = null;
        var vm = new GlobalSearchViewModel(state, openTask: t => openedTask = t, openNote: null, openBoard: null, openTag: null);
        vm.Query = "Task One";
        Assert.True(vm.HasTaskResults);
        var taskHit = vm.TaskHits[0];
        vm.OpenTaskHitCommand.Execute(taskHit);
        Assert.NotNull(openedTask);
        Assert.Equal("t1", openedTask!.Id);
        Assert.Equal(string.Empty, vm.Query);
        Assert.False(vm.IsOpen);
    }

    [Fact]
    public void SelectingNote_RoutesToCorrectAction()
    {
        var state = CreateState();
        Note? openedNote = null;
        var vm = new GlobalSearchViewModel(state, openTask: null, openNote: n => openedNote = n, openBoard: null, openTag: null);
        vm.Query = "Note One";
        Assert.True(vm.HasNoteResults);
        var noteHit = vm.NoteHits[0];
        vm.OpenNoteHitCommand.Execute(noteHit);
        Assert.NotNull(openedNote);
        Assert.Equal("n1", openedNote!.Id);
    }

    [Fact]
    public void SelectingBoard_RoutesToCorrectAction()
    {
        var state = CreateState();
        Board? openedBoard = null;
        var vm = new GlobalSearchViewModel(state, openTask: null, openNote: null, openBoard: b => openedBoard = b, openTag: null);
        vm.Query = "Board Two";
        Assert.True(vm.HasBoardResults);
        var boardHit = vm.BoardHits.First(b => b.Name == "Board Two");
        vm.OpenBoardHitCommand.Execute(boardHit);
        Assert.NotNull(openedBoard);
        Assert.Equal("b2", openedBoard!.Id);
    }

    [Fact]
    public void SelectingTag_RoutesToCorrectAction()
    {
        var state = CreateState();
        string? openedTag = null;
        var vm = new GlobalSearchViewModel(state, openTask: null, openNote: null, openBoard: null, openTag: t => openedTag = t);
        vm.Query = "bug";
        Assert.True(vm.HasTagResults);
        var tagHit = vm.TagHits[0];
        vm.OpenTagHitCommand.Execute(tagHit);
        Assert.NotNull(openedTag);
        Assert.Equal("bug", openedTag);
    }

    [Fact]
    public void ExecuteSelected_UsesCurrentSelection()
    {
        var state = CreateState();
        TaskItem? opened = null;
        var vm = new GlobalSearchViewModel(state, openTask: t => opened = t, openNote: null, openBoard: null, openTag: null);
        vm.Query = "Task One";
        Assert.Equal(0, vm.SelectedIndex);
        vm.ExecuteSelectedCommand.Execute(null);
        Assert.NotNull(opened);
        Assert.Equal("t1", opened!.Id);
    }

    [Fact]
    public void MainWindowViewModel_TagNavigation_FiltersTasks()
    {
        var state = CreateState();
        var kanBan = new KanBanViewModel(state);
        var tasks = new TasksViewModel(state, kanBan);
        var notes = new NotesViewModel(state);
        var main = new MainWindowViewModel(state, kanBan, tasks, notes, null, null, null, null);
        // Search for tag "bug" should filter tasks
        main.GlobalSearch.Query = "bug";
        Assert.True(main.GlobalSearch.HasTagResults);
        var tagHit = main.GlobalSearch.TagHits[0];
        // Simulate opening tag via ViewModel (which will call MainWindow's OpenTagFromSearch)
        main.GlobalSearch.OpenTagHitCommand.Execute(tagHit);
        Assert.Equal("bug", tasks.SearchText);
        Assert.Equal(tasks, main.CurrentSection);
    }

    [Fact]
    public void MainWindowViewModel_NoteNavigation_SelectsNoteAndNavigates()
    {
        var state = CreateState();
        var kanBan = new KanBanViewModel(state);
        var tasks = new TasksViewModel(state, kanBan);
        var notes = new NotesViewModel(state);
        var main = new MainWindowViewModel(state, kanBan, tasks, notes, null, null, null, null);
        var note = state.Notes[0];
        main.GlobalSearch.Query = "Note One";
        var hit = main.GlobalSearch.NoteHits[0];
        main.GlobalSearch.OpenNoteHitCommand.Execute(hit);
        Assert.Equal(note.Id, notes.SelectedNote?.Id);
        Assert.Equal(notes, main.CurrentSection);
    }

    [Fact]
    public void MainWindowViewModel_BoardNavigation_SelectsBoard()
    {
        var state = CreateState();
        var kanBan = new KanBanViewModel(state);
        var tasks = new TasksViewModel(state, kanBan);
        var notes = new NotesViewModel(state);
        var main = new MainWindowViewModel(state, kanBan, tasks, notes, null, null, null, null);
        main.GlobalSearch.Query = "Board Two";
        var hit = main.GlobalSearch.BoardHits.First(b => b.Name == "Board Two");
        main.GlobalSearch.OpenBoardHitCommand.Execute(hit);
        Assert.Equal("b2", state.SelectedBoard?.Id);
        Assert.Equal(kanBan, main.CurrentSection);
    }
}

