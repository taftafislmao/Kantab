using KanTab.Models;
using KanTab.Storage;
using KanTab.ViewModels;
using Xunit;

namespace KanTab.Tests;

public class BoardManagementTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "KanTab.Tests", "BoardManagement", Guid.NewGuid().ToString());

    private string FilePath => Path.Combine(_directory, "kantab.json");

    public BoardManagementTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            try { Directory.Delete(_directory, recursive: true); }
            catch { }
        }
    }

    private LocalJsonKanTabRepository Repo() => new(FilePath);

    [Fact]
    public void SampleDataSeeder_CreateDefaultBoard_HasDefaultColumns()
    {
        var board = SampleDataSeeder.CreateDefaultBoard("My New Board");

        Assert.Equal("My New Board", board.Name);
        Assert.Equal(3, board.Columns.Count);
        Assert.Equal("To Do", board.Columns[0].Title);
        Assert.Equal("In Progress", board.Columns[1].Title);
        Assert.Equal("Done", board.Columns[2].Title);
        Assert.Equal(0, board.Columns[0].Position);
        Assert.Equal(1, board.Columns[1].Position);
        Assert.Equal(2, board.Columns[2].Position);
    }

    [Fact]
    public void SampleDataSeeder_DuplicateBoard_PreservesStructureAndNewIds()
    {
        var source = new Board { Id = "src1", Name = "Original", Position = 0 };
        var col1 = new KanBanColumn { Id = "col1", Title = "Col1", BoardId = source.Id, Position = 0 };
        var col2 = new KanBanColumn { Id = "col2", Title = "Col2", BoardId = source.Id, Position = 1 };
        col1.Tasks.Add(new TaskItem { Id = "t1", Title = "Task 1", BoardId = source.Id, ColumnId = col1.Id });
        col1.Tasks.Add(new TaskItem { Id = "t2", Title = "Task 2", BoardId = source.Id, ColumnId = col1.Id });
        col2.Tasks.Add(new TaskItem { Id = "t3", Title = "Task 3", BoardId = source.Id, ColumnId = col2.Id });
        source.Columns.Add(col1);
        source.Columns.Add(col2);

        var copy = SampleDataSeeder.DuplicateBoard(source, "Copy");

        Assert.Equal("Copy", copy.Name);
        Assert.NotEqual(source.Id, copy.Id);
        Assert.Equal(2, copy.Columns.Count);
        Assert.Equal("Col1", copy.Columns[0].Title);
        Assert.Equal("Col2", copy.Columns[1].Title);
        Assert.NotEqual(col1.Id, copy.Columns[0].Id);
        Assert.NotEqual(col2.Id, copy.Columns[1].Id);
        Assert.Equal(copy.Id, copy.Columns[0].BoardId);
        Assert.Equal(copy.Id, copy.Columns[1].BoardId);
        Assert.Equal(copy.Columns[0].Id, copy.Columns[0].Tasks[0].ColumnId);
        Assert.Equal(copy.Id, copy.Columns[0].Tasks[0].BoardId);
        Assert.Equal(2, copy.Columns[0].Tasks.Count);
        Assert.Equal(1, copy.Columns[1].Tasks.Count);
        Assert.NotEqual("t1", copy.Columns[0].Tasks[0].Id);
        Assert.Equal(0, copy.Columns[0].Tasks[0].Position);
        Assert.Equal(1, copy.Columns[0].Tasks[1].Position);
    }

    [Fact]
    public void Board_IsArchived_IsTrueOnlyWhenArchived()
    {
        var board = new Board { Name = "Test" };
        Assert.False(board.IsArchived);
        Assert.False(board.IsDeleted);

        board.ArchivedAt = DateTime.Now;
        Assert.True(board.IsArchived);
        Assert.False(board.IsDeleted);

        board.DeletedAt = DateTime.Now;
        Assert.True(board.IsDeleted);
    }

    [Fact]
    public void Board_RestoreBoard_ClearsArchivedAt()
    {
        var board = new Board { Name = "Test", ArchivedAt = DateTime.Now };
        Assert.True(board.IsArchived);

        board.ArchivedAt = null;
        Assert.False(board.IsArchived);
    }

    [Fact]
    public void Board_ArchivedAndDeletedStates()
    {
        var board = new Board { Name = "Test" };
        Assert.False(board.IsArchived);
        Assert.False(board.IsDeleted);

        board.ArchivedAt = DateTime.Now;
        Assert.True(board.IsArchived);
        Assert.False(board.IsDeleted);

        board.DeletedAt = DateTime.Now;
        board.ArchivedAt = null;
        Assert.False(board.IsArchived);
        Assert.True(board.IsDeleted);
    }

    [Fact]
    public void WorkspaceState_DeleteBoard_RemovesFromCollection()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        var board = state.Boards[0];

        state.Boards.Remove(board);

        Assert.Empty(state.Boards);
        Assert.True(state.SaveNow());

        // Reload will seed sample data because the file is now empty,
        // but our removed board should not reappear.
        var reloaded = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        Assert.DoesNotContain(reloaded.Boards, b => b.Id == board.Id);
    }

    [Fact]
    public void WorkspaceState_ArchiveBoard_PersistsArchivedAt()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        var board = state.Boards[0];

        board.ArchivedAt = DateTime.Now;
        state.NotifyChanged();

        Assert.True(board.IsArchived);
        Assert.True(state.SaveNow());

        var reloaded = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        Assert.True(reloaded.Boards[0].IsArchived);
    }
}
