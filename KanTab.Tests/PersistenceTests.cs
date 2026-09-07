using KanTab.Models;
using KanTab.Storage;
using KanTab.ViewModels;

namespace KanTab.Tests;

public class PersistenceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "KanTab.Tests", Guid.NewGuid().ToString());

    private string FilePath => Path.Combine(_directory, "kantab.json");

    public PersistenceTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            try
            {
                Directory.Delete(_directory, recursive: true);
            }
            catch
            {
                // Best-effort cleanup only.
            }
        }
    }

    private LocalJsonKanTabRepository Repo() => new(FilePath);

    [Fact]
    public void Load_MissingFile_ReturnsEmptyData()
    {
        var data = Repo().LoadAsync().GetAwaiter().GetResult();

        Assert.Empty(data.Boards);
        Assert.Empty(data.Notes);
        Assert.Null(Repo().LastLoadError);
    }

    [Fact]
    public void Load_EmptyFile_ReturnsEmptyData()
    {
        File.WriteAllText(FilePath, "   ");

        var data = Repo().LoadAsync().GetAwaiter().GetResult();

        Assert.Empty(data.Boards);
        Assert.Empty(data.Notes);
    }

    [Fact]
    public void Load_CorruptFile_DoesNotCrashAndDoesNotOverwrite()
    {
        File.WriteAllText(FilePath, "{ this is not valid json !!!");

        var repo = Repo();
        var data = repo.LoadAsync().GetAwaiter().GetResult();

        Assert.Empty(data.Boards);
        Assert.NotNull(repo.LastLoadError);
        Assert.Equal("{ this is not valid json !!!", File.ReadAllText(FilePath));
    }

    [Fact]
    public void BoardColumnTask_RoundTrip_PreservesAllFields()
    {
        var due = new DateOnly(2026, 9, 10);
        var board = new Board { Id = "b1", Name = "Board", Position = 0, CreatedAt = new DateTime(2024, 1, 1), UpdatedAt = new DateTime(2024, 1, 2) };
        var column = new KanBanColumn { Id = "c1", Title = "Cards", Position = 0, CreatedAt = new DateTime(2024, 1, 1), UpdatedAt = new DateTime(2024, 1, 2) };
        column.Tasks.Add(new TaskItem { Id = "t1", BoardId = "b1", ColumnId = "c1", Title = "Task", Description = "Desc", Priority = Priority.High, DueDate = due, IsCompleted = true, Position = 0, Tags = new List<string> { "tag1", "tag2" }, CreatedAt = new DateTime(2024, 1, 1), UpdatedAt = new DateTime(2024, 1, 2) });
        board.Columns.Add(column);

        Repo().SaveAsync(new KanTabData { Boards = new List<Board> { board } }).GetAwaiter().GetResult();

        var loaded = Repo().LoadAsync().GetAwaiter().GetResult();

        var b = Assert.Single(loaded.Boards);
        Assert.Equal("Board", b.Name);
        var c = Assert.Single(b.Columns);
        Assert.Equal("Cards", c.Title);
        var t = Assert.Single(c.Tasks);
        Assert.Equal("Task", t.Title);
        Assert.Equal("Desc", t.Description);
        Assert.Equal(Priority.High, t.Priority);
        Assert.Equal(due, t.DueDate);
        Assert.True(t.IsCompleted);
        Assert.Equal(new[] { "tag1", "tag2" }, t.Tags);
        Assert.Equal("b1", t.BoardId);
        Assert.Equal("c1", t.ColumnId);
    }

    [Fact]
    public void Notes_RoundTrip_PreservesAllFields()
    {
        var doc = new KanTabData
        {
            Notes = new List<Note>
            {
                new() { Id = "n1", Title = "First", Content = "Hello", IsPinned = true, CreatedAt = new DateTime(2024, 1, 1), UpdatedAt = new DateTime(2024, 1, 2) }
            }
        };
        Repo().SaveAsync(doc).GetAwaiter().GetResult();

        var loaded = Repo().LoadAsync().GetAwaiter().GetResult();

        var n = Assert.Single(loaded.Notes);
        Assert.Equal("First", n.Title);
        Assert.Equal("Hello", n.Content);
        Assert.True(n.IsPinned);
    }

    [Fact]
    public void TaskOrdering_SurvivesRoundTrip()
    {
        var board = new Board { Id = "b1", Name = "Board" };
        var column = new KanBanColumn { Id = "c1", Title = "Cards" };
        for (var i = 0; i < 3; i++)
            column.Tasks.Add(new TaskItem { Id = $"t{i}", Title = $"T{i}", ColumnId = "c1", Position = i });
        board.Columns.Add(column);
        Repo().SaveAsync(new KanTabData { Boards = new List<Board> { board } }).GetAwaiter().GetResult();

        var loaded = Repo().LoadAsync().GetAwaiter().GetResult();

        var tasks = loaded.Boards[0].Columns[0].Tasks.OrderBy(t => t.Position).ToList();
        Assert.Equal(new[] { "t0", "t1", "t2" }, tasks.Select(t => t.Id));
    }

    [Fact]
    public void Sanitize_RepairsDuplicateAndMissingIds()
    {
        var data = new KanTabData();
        var board = new Board { Id = "dup", Name = "A" };
        board.Columns.Add(new KanBanColumn { Id = "", Title = "C1" });
        board.Columns.Add(new KanBanColumn { Id = "dup", Title = "C2" });
        board.Columns[0].Tasks.Add(new TaskItem { Title = "T", ColumnId = "wrong" });
        var board2 = new Board { Id = "dup", Name = "B" };
        data.Boards.Add(board);
        data.Boards.Add(board2);
        data.Notes.Add(new Note { Id = "", Title = "N" });

        var sane = LocalJsonKanTabRepository.Sanitize(data);

        var boardIds = sane.Boards.Select(b => b.Id).ToList();
        Assert.Equal(2, boardIds.Distinct().Count());
        var allColumnIds = sane.Boards.SelectMany(b => b.Columns).Select(c => c.Id).ToList();
        Assert.All(allColumnIds, id => Assert.False(string.IsNullOrWhiteSpace(id)));
        Assert.Equal(2, allColumnIds.Distinct().Count());
        var task = sane.Boards[0].Columns[0].Tasks[0];
        Assert.Equal(sane.Boards[0].Columns[0].Id, task.ColumnId);
        Assert.Equal(sane.Boards[0].Id, task.BoardId);
        Assert.Equal(0, sane.Boards[0].Columns[0].Position);
        Assert.Equal(1, sane.Boards[0].Columns[1].Position);
        Assert.False(string.IsNullOrWhiteSpace(sane.Notes[0].Id));
    }

    [Fact]
    public void WorkspaceState_FirstLaunch_SeedsSampleDataOnce()
    {
        var state1 = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        Assert.Single(state1.Boards);
        Assert.True(File.Exists(FilePath));

        state1.Boards[0].Name = "User Renamed";
        state1.SaveNow();

        var state2 = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        Assert.Equal("User Renamed", state2.Boards[0].Name);
    }

    [Fact]
    public void WorkspaceState_ExistingData_LoadsInsteadOfSeeding()
    {
        var userBoard = new Board { Id = "mine", Name = "Mine", Position = 0 };
        Repo().SaveAsync(new KanTabData { Boards = new List<Board> { userBoard } }).GetAwaiter().GetResult();

        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);

        var board = Assert.Single(state.Boards);
        Assert.Equal("Mine", board.Name);
        Assert.Empty(board.Columns);
    }

    [Fact]
    public void WorkspaceState_CorruptFile_FallsBackWithoutSeeding()
    {
        File.WriteAllText(FilePath, "not json at all");

        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);

        Assert.Empty(state.Boards);
        Assert.NotNull(state.LoadError);
        Assert.NotNull(state.StorageError);
        Assert.Equal("not json at all", File.ReadAllText(FilePath));
    }

    [Fact]
    public void MoveTask_UpdatesColumnAndPositionAndPersists()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        var board = state.Boards[0];
        var source = board.Columns[0];
        var dest = board.Columns[1];
        var task = source.Tasks[0];

        var vm = new KanBanViewModel(state);
        vm.MoveTaskCommand.Execute((task, dest.Id));

        Assert.Equal(dest.Id, task.ColumnId);
        Assert.Contains(task, dest.Tasks);
        Assert.DoesNotContain(task, source.Tasks);
        Assert.Equal(dest.Tasks.Select(t => t.Position), Enumerable.Range(0, dest.Tasks.Count));
        Assert.Equal(source.Tasks.Select(t => t.Position), Enumerable.Range(0, source.Tasks.Count));
        Assert.True(state.SaveNow());

        var reloaded = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        var reloadedDest = reloaded.Boards[0].Columns[1];
        Assert.Contains(reloadedDest.Tasks, t => t.Id == task.Id);
    }

    [Fact]
    public void TypedDueDate_SerializesAsIsoDate()
    {
        var board = new Board { Id = "b1", Name = "B" };
        var col = new KanBanColumn { Id = "c1", Title = "C" };
        col.Tasks.Add(new TaskItem { Id = "t1", Title = "T", DueDate = new DateOnly(2026, 9, 10) });
        board.Columns.Add(col);

        Repo().SaveAsync(new KanTabData { Boards = new List<Board> { board } }).GetAwaiter().GetResult();

        Assert.Contains("2026-09-10", File.ReadAllText(FilePath));

        var loaded = Repo().LoadAsync().GetAwaiter().GetResult();
        Assert.Equal(new DateOnly(2026, 9, 10), loaded.Boards[0].Columns[0].Tasks[0].DueDate);
    }
}
