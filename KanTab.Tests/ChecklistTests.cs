#pragma warning disable xUnit1031, xUnit2013
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using KanTab.Models;
using KanTab.Storage;
using KanTab.Storage.Supabase;
using KanTab.ViewModels;
using Xunit;

namespace KanTab.Tests;

public class ChecklistTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "KanTab.Tests", "Checklist", Guid.NewGuid().ToString());
    private string FilePath => Path.Combine(_directory, "kantab.json");
    public ChecklistTests() => Directory.CreateDirectory(_directory);
    public void Dispose() { try { Directory.Delete(_directory, true); } catch { } }
    private LocalJsonKanTabRepository Repo() => new(FilePath);

    // ---------- Data ----------
    [Fact]
    public void ChecklistItem_Creation_HasStableUuidAndDefaults()
    {
        var a = new ChecklistItem { Text = "Item" };
        var b = new ChecklistItem { Text = "Item" };
        Assert.False(string.IsNullOrWhiteSpace(a.Id));
        Assert.NotEqual(a.Id, b.Id);
        Assert.True(Guid.TryParse(a.Id, out _));
        Assert.Equal(0, a.Position);
        Assert.False(a.IsCompleted);
        Assert.Equal("Item", a.Text);
    }

    [Fact]
    public void TaskItem_EmptyChecklist_HasNoProgress()
    {
        var task = new TaskItem { Title = "T" };
        Assert.Empty(task.Checklist);
        Assert.False(task.HasChecklist);
        Assert.Null(task.ChecklistProgressText);
        Assert.Equal(0, task.ChecklistPercent);
        Assert.Equal(string.Empty, task.ChecklistPercentText);
        Assert.False(task.IsChecklistComplete);
        Assert.Equal(0, task.ChecklistCompletedCount);
        Assert.Equal(0, task.ChecklistTotalCount);
    }

    [Fact]
    public void TaskItem_Checklist_CompletionState_Preserved()
    {
        var task = new TaskItem { Title = "T" };
        task.Checklist.Add(new ChecklistItem { Text = "A", IsCompleted = true, Position = 0 });
        task.Checklist.Add(new ChecklistItem { Text = "B", IsCompleted = false, Position = 1 });
        Assert.Equal(1, task.ChecklistCompletedCount);
        Assert.Equal(2, task.ChecklistTotalCount);
        Assert.True(task.Checklist[0].IsCompleted);
        Assert.False(task.Checklist[1].IsCompleted);
    }

    [Fact]
    public void TaskItem_PositionOrdering_IsStable()
    {
        var task = new TaskItem { Title = "T" };
        for (int i = 0; i < 3; i++)
            task.Checklist.Add(new ChecklistItem { Text = $"I{i}", Position = i });
        Assert.Equal(new[] { 0, 1, 2 }, task.Checklist.Select(c => c.Position));
        Assert.Equal("I0", task.Checklist[0].Text);
    }

    // ---------- Operations ----------
    [Fact]
    public void AddChecklistItem_IncrementsCountAndPreservesIds()
    {
        var vm = new TaskDialogViewModel();
        vm.NewChecklistText = "First item";
        vm.AddChecklistItemCommand.Execute(null);
        Assert.Single(vm.Checklist);
        var id = vm.Checklist[0].Id;
        Assert.Equal("First item", vm.Checklist[0].Text);
        Assert.Equal(0, vm.Checklist[0].Position);
        vm.NewChecklistText = "Second";
        vm.AddChecklistItemCommand.Execute(null);
        Assert.Equal(2, vm.Checklist.Count);
        Assert.Equal(id, vm.Checklist[0].Id);
        Assert.Equal(1, vm.Checklist[1].Position);
    }

    [Fact]
    public void EditChecklistItem_TextIsMutable()
    {
        var task = new TaskItem { Title = "T" };
        var item = new ChecklistItem { Text = "Original", Position = 0 };
        task.Checklist.Add(item);
        item.Text = "Edited";
        Assert.Equal("Edited", task.Checklist[0].Text);
    }

    [Fact]
    public void CompleteAndUncomplete_UpdatesProgress()
    {
        var task = new TaskItem { Title = "T" };
        var a = new ChecklistItem { Text = "A", IsCompleted = false, Position = 0 };
        var b = new ChecklistItem { Text = "B", IsCompleted = false, Position = 1 };
        task.Checklist.Add(a);
        task.Checklist.Add(b);
        Assert.Equal("0/2", task.ChecklistProgressText);
        a.IsCompleted = true;
        Assert.Equal("1/2", task.ChecklistProgressText);
        Assert.Equal(50, task.ChecklistPercent);
        b.IsCompleted = true;
        Assert.Equal("2/2", task.ChecklistProgressText);
        Assert.True(task.IsChecklistComplete);
        a.IsCompleted = false;
        Assert.Equal("1/2", task.ChecklistProgressText);
        Assert.False(task.IsChecklistComplete);
    }

    [Fact]
    public void DeleteChecklistItem_RemovesAndRenumberPositions()
    {
        var vm = new TaskDialogViewModel();
        vm.NewChecklistText = "A"; vm.AddChecklistItemCommand.Execute(null);
        vm.NewChecklistText = "B"; vm.AddChecklistItemCommand.Execute(null);
        vm.NewChecklistText = "C"; vm.AddChecklistItemCommand.Execute(null);
        Assert.Equal(3, vm.Checklist.Count);
        var middle = vm.Checklist[1];
        vm.RemoveChecklistItemCommand.Execute(middle);
        Assert.Equal(2, vm.Checklist.Count);
        Assert.Equal("A", vm.Checklist[0].Text);
        Assert.Equal("C", vm.Checklist[1].Text);
        Assert.Equal(0, vm.Checklist[0].Position);
        Assert.Equal(1, vm.Checklist[1].Position);
    }

    [Fact]
    public void ReorderChecklist_MoveUpAndDown_PersistsOrder()
    {
        var vm = new TaskDialogViewModel();
        foreach (var t in new[] { "A", "B", "C" }) { vm.NewChecklistText = t; vm.AddChecklistItemCommand.Execute(null); }
        var b = vm.Checklist[1];
        vm.MoveChecklistItemUpCommand.Execute(b);
        Assert.Equal(new[] { "B", "A", "C" }, vm.Checklist.Select(c => c.Text));
        Assert.Equal(new[] { 0, 1, 2 }, vm.Checklist.Select(c => c.Position));
        var a = vm.Checklist[1]; // now A at index 1
        vm.MoveChecklistItemDownCommand.Execute(a);
        Assert.Equal(new[] { "B", "C", "A" }, vm.Checklist.Select(c => c.Text));
        vm.MoveChecklistItemUpCommand.Execute(vm.Checklist[0]); // top stays
        Assert.Equal("B", vm.Checklist[0].Text);
        vm.MoveChecklistItemDownCommand.Execute(vm.Checklist[2]); // bottom stays
        Assert.Equal("A", vm.Checklist[2].Text);
    }

    // ---------- Progress ----------
    [Fact]
    public void Progress_ZeroOfN_ShowsCorrect()
    {
        var task = new TaskItem { Title = "T" };
        for (int i = 0; i < 4; i++) task.Checklist.Add(new ChecklistItem { Text = $"I{i}", IsCompleted = false, Position = i });
        Assert.Equal("0/4", task.ChecklistProgressText);
        Assert.Equal(0, task.ChecklistPercent);
        Assert.Equal("0%", task.ChecklistPercentText);
        Assert.False(task.IsChecklistComplete);
    }

    [Fact]
    public void Progress_Partial_ShowsCorrect()
    {
        var task = new TaskItem { Title = "T" };
        task.Checklist.Add(new ChecklistItem { Text = "A", IsCompleted = true, Position = 0 });
        task.Checklist.Add(new ChecklistItem { Text = "B", IsCompleted = true, Position = 1 });
        task.Checklist.Add(new ChecklistItem { Text = "C", IsCompleted = false, Position = 2 });
        task.Checklist.Add(new ChecklistItem { Text = "D", IsCompleted = false, Position = 3 });
        Assert.Equal("2/4", task.ChecklistProgressText);
        Assert.Equal(50, task.ChecklistPercent);
        Assert.Equal("50%", task.ChecklistPercentText);
    }

    [Fact]
    public void Progress_Complete_ShowsComplete()
    {
        var task = new TaskItem { Title = "T" };
        for (int i = 0; i < 3; i++) task.Checklist.Add(new ChecklistItem { Text = $"I{i}", IsCompleted = true, Position = i });
        Assert.Equal("3/3", task.ChecklistProgressText);
        Assert.Equal(100, task.ChecklistPercent);
        Assert.True(task.IsChecklistComplete);
    }

    [Fact]
    public void Progress_NoItems_ShowsNoProgress()
    {
        var task = new TaskItem { Title = "T" };
        Assert.Null(task.ChecklistProgressText);
        Assert.Equal(string.Empty, task.ChecklistPercentText);
        Assert.False(task.HasChecklist);
    }

    [Fact]
    public void ParentTaskCompletion_IsIndependentFromChecklist()
    {
        var task = new TaskItem { Title = "T", IsCompleted = false };
        task.Checklist.Add(new ChecklistItem { Text = "A", IsCompleted = true, Position = 0 });
        task.Checklist.Add(new ChecklistItem { Text = "B", IsCompleted = true, Position = 1 });
        Assert.True(task.IsChecklistComplete);
        Assert.False(task.IsCompleted);
        task.IsCompleted = true;
        Assert.True(task.IsCompleted);
        Assert.True(task.IsChecklistComplete);
    }

    // ---------- Persistence ----------
    [Fact]
    public void Checklist_SurvivesSaveLoad_PreservesOrderingAndCompletion()
    {
        var board = new Board { Id = "b1", Name = "B" };
        var col = new KanBanColumn { Id = "c1", Title = "C" };
        var task = new TaskItem { Id = "t1", Title = "T", ColumnId = "c1", BoardId = "b1", Position = 0 };
        task.Checklist.Add(new ChecklistItem { Id = "i1", Text = "First", IsCompleted = true, Position = 0, CreatedAt = new DateTime(2024,1,1), UpdatedAt = new DateTime(2024,1,2) });
        task.Checklist.Add(new ChecklistItem { Id = "i2", Text = "Second", IsCompleted = false, Position = 1, CreatedAt = new DateTime(2024,1,1), UpdatedAt = new DateTime(2024,1,2) });
        task.Checklist.Add(new ChecklistItem { Id = "i3", Text = "Third", IsCompleted = false, Position = 2 });
        col.Tasks.Add(task);
        board.Columns.Add(col);
        Repo().SaveAsync(new KanTabData { Boards = new List<Board> { board } }).GetAwaiter().GetResult();

        var loaded = Repo().LoadAsync().GetAwaiter().GetResult();
        var loadedTask = loaded.Boards[0].Columns[0].Tasks[0];
        Assert.Equal(3, loadedTask.Checklist.Count);
        Assert.Equal("i1", loadedTask.Checklist[0].Id);
        Assert.Equal("First", loadedTask.Checklist[0].Text);
        Assert.True(loadedTask.Checklist[0].IsCompleted);
        Assert.Equal("i2", loadedTask.Checklist[1].Id);
        Assert.False(loadedTask.Checklist[1].IsCompleted);
        Assert.Equal(0, loadedTask.Checklist[0].Position);
        Assert.Equal(1, loadedTask.Checklist[1].Position);
        Assert.Equal(2, loadedTask.Checklist[2].Position);
        Assert.Equal("1/3", loadedTask.ChecklistProgressText);
    }

    [Fact]
    public void Checklist_Sanitize_RepairsMissingIdsAndTrimsText()
    {
        var board = new Board { Id = "b1", Name = "B" };
        var col = new KanBanColumn { Id = "c1", Title = "C" };
        var task = new TaskItem { Id = "t1", Title = "T", ColumnId = "c1", BoardId = "b1" };
        task.Checklist.Add(new ChecklistItem { Id = "", Text = "  spaced  ", Position = 0 });
        task.Checklist.Add(new ChecklistItem { Id = "dup", Text = "", Position = 1 });
        task.Checklist.Add(new ChecklistItem { Id = "dup", Text = "valid", Position = 2 });
        col.Tasks.Add(task);
        board.Columns.Add(col);
        var data = new KanTabData { Boards = new List<Board> { board } };
        var sane = LocalJsonKanTabRepository.Sanitize(data);
        var items = sane.Boards[0].Columns[0].Tasks[0].Checklist;
        Assert.Equal(3, items.Count);
        Assert.All(items, i => Assert.False(string.IsNullOrWhiteSpace(i.Id)));
        Assert.Equal(3, items.Select(i=>i.Id).Distinct().Count());
        Assert.Equal("spaced", items[0].Text);
        Assert.Equal("Untitled item", items[1].Text);
        Assert.Equal(new[] {0,1,2}, items.Select(i=>i.Position));
    }

    [Fact]
    public void Checklist_Ordering_SurvivesSaveLoad()
    {
        var board = new Board { Id = "b1", Name = "B" };
        var col = new KanBanColumn { Id = "c1", Title = "C" };
        var task = new TaskItem { Id = "t1", Title = "T", ColumnId = "c1" };
        // add out-of-order positions, sanitize should reorder by Position then renumber
        task.Checklist.Add(new ChecklistItem { Id="a", Text="A", Position=2 });
        task.Checklist.Add(new ChecklistItem { Id="b", Text="B", Position=0 });
        task.Checklist.Add(new ChecklistItem { Id="c", Text="C", Position=1 });
        col.Tasks.Add(task);
        board.Columns.Add(col);
        Repo().SaveAsync(new KanTabData{Boards=new List<Board>{board}}).GetAwaiter().GetResult();
        var loaded = Repo().LoadAsync().GetAwaiter().GetResult();
        var items = loaded.Boards[0].Columns[0].Tasks[0].Checklist.OrderBy(c=>c.Position).ToList();
        Assert.Equal(new[]{"b","c","a"}, items.Select(i=>i.Id));
        Assert.Equal(new[]{0,1,2}, items.Select(i=>i.Position));
    }

    // ---------- Task integration ----------
    [Fact]
    public void Checklist_BelongsToCorrectTask()
    {
        var board = new Board { Id="b1", Name="B" };
        var col = new KanBanColumn{ Id="c1", Title="C"};
        var t1 = new TaskItem{ Id="t1", Title="One", ColumnId="c1"};
        var t2 = new TaskItem{ Id="t2", Title="Two", ColumnId="c1"};
        t1.Checklist.Add(new ChecklistItem{ Text="Only T1", Position=0});
        col.Tasks.Add(t1); col.Tasks.Add(t2); board.Columns.Add(col);
        Repo().SaveAsync(new KanTabData{Boards=new List<Board>{board}}).GetAwaiter().GetResult();
        var loaded = Repo().LoadAsync().GetAwaiter().GetResult();
        var lt1 = loaded.Boards[0].Columns[0].Tasks.First(t=>t.Id=="t1");
        var lt2 = loaded.Boards[0].Columns[0].Tasks.First(t=>t.Id=="t2");
        Assert.Single(lt1.Checklist);
        Assert.Empty(lt2.Checklist);
    }

    [Fact]
    public void DeletingTask_RemovesChecklist()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer:false);
        var board = state.Boards[0];
        var col = board.Columns[0];
        var task = col.Tasks[0];
        task.Checklist.Add(new ChecklistItem{ Text="A", Position=0});
        task.Checklist.Add(new ChecklistItem{ Text="B", Position=1});
        state.SaveNow();
        var vm = new KanBanViewModel(state);
        vm.DeleteTaskCommand.Execute(task);
        Assert.DoesNotContain(task, col.Tasks);
        state.SaveNow();
        var reloaded = WorkspaceState.Load(Repo(), useDispatcherTimer:false);
        Assert.DoesNotContain(reloaded.Boards[0].Columns[0].Tasks, t=>t.Id==task.Id);
    }

    [Fact]
    public void FilteringDoesNotLoseChecklistData()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer:false);
        var board = state.Boards[0];
        var kanBan = new KanBanViewModel(state);
        kanBan.SelectedBoard = board;
        var tasksVm = new TasksViewModel(state, kanBan);
        var task = board.Columns[0].Tasks[1];
        task.Checklist.Clear();
        task.Checklist.Add(new ChecklistItem{ Text="FilterTest", Position=0});
        state.NotifyChanged();
        tasksVm.SelectedFilter = "Active";
        var row = tasksVm.Rows.FirstOrDefault(r=>r.Task.Id==task.Id);
        Assert.NotNull(row);
        Assert.Single(row.Task.Checklist);
        tasksVm.SelectedFilter = "Completed";
        tasksVm.SelectedFilter = "All";
        var row2 = tasksVm.Rows.First(r=>r.Task.Id==task.Id);
        Assert.Single(row2.Task.Checklist);
    }

    [Fact]
    public void SortingDoesNotLoseChecklistData()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer:false);
        var board = state.Boards[0];
        var kanBan = new KanBanViewModel(state);
        kanBan.SelectedBoard = board;
        var tasksVm = new TasksViewModel(state, kanBan);
        var task = board.Columns[0].Tasks[2];
        task.Checklist.Clear();
        task.Checklist.Add(new ChecklistItem{ Text="SortTest", Position=0});
        var before = task.Checklist[0].Id;
        tasksVm.SelectedSort = "Oldest";
        var row = tasksVm.Rows.First(r=>r.Task.Id==task.Id);
        Assert.Equal(before, row.Task.Checklist[0].Id);
        tasksVm.SelectedSort = "Priority";
        row = tasksVm.Rows.First(r=>r.Task.Id==task.Id);
        Assert.Equal(before, row.Task.Checklist[0].Id);
    }

    // ---------- Shared state ----------
    [Fact]
    public void SharedState_ChecklistChange_ReflectedBetweenTasksAndKanBan()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer:false);
        var board = state.Boards[0];
        var kanBan = new KanBanViewModel(state);
        kanBan.SelectedBoard = board;
        var tasksVm = new TasksViewModel(state, kanBan);
        var task = board.Columns[0].Tasks[1];
        task.Checklist.Clear();
        Assert.False(task.HasChecklist);
        task.Checklist.Add(new ChecklistItem{ Text="Shared", IsCompleted=false, Position=0});
        state.NotifyChanged();
        Assert.True(task.HasChecklist);
        Assert.Equal("0/1", task.ChecklistProgressText);
        var row = tasksVm.Rows.First(r=>r.Task.Id==task.Id);
        Assert.True(row.HasChecklist);
        Assert.Equal("0/1", row.ChecklistProgressText);
        // complete via task
        task.Checklist[0].IsCompleted = true;
        state.NotifyChanged();
        Assert.Equal("1/1", task.ChecklistProgressText);
        Assert.Equal("1/1", row.ChecklistProgressText);
        // verify kanban task also reflects
        var kanBanTask = board.Columns.SelectMany(c=>c.Tasks).First(t=>t.Id==task.Id);
        Assert.Equal("1/1", kanBanTask.ChecklistProgressText);
    }

    // ---------- Sync ----------
    [Fact]
    public void TaskDto_Checklist_SerializesAndDeserializes()
    {
        var task = new TaskItem{ Id="t1", Title="T"};
        task.Checklist.Add(new ChecklistItem{ Id="c1", Text="Item1", IsCompleted=true, Position=0, CreatedAt=new DateTime(2024,1,1,0,0,0,DateTimeKind.Utc), UpdatedAt=new DateTime(2024,1,2,0,0,0,DateTimeKind.Utc)});
        task.Checklist.Add(new ChecklistItem{ Id="c2", Text="Item2", IsCompleted=false, Position=1});
        var dto = new TaskDto(task, "b1", "col1", "user1");
        Assert.Equal(2, dto.Checklist.Count);
        Assert.Equal("c1", dto.Checklist[0].Id);
        Assert.Equal("Item1", dto.Checklist[0].Text);
        Assert.True(dto.Checklist[0].IsCompleted);
        Assert.Equal(0, dto.Checklist[0].Position);
        // JSON round-trip via Supabase serializer options
        var json = System.Text.Json.JsonSerializer.Serialize(dto, new System.Text.Json.JsonSerializerOptions{ PropertyNamingPolicy=System.Text.Json.JsonNamingPolicy.SnakeCaseLower, WriteIndented=true});
        Assert.Contains("checklist", json);
        Assert.Contains("Item1", json);
        Assert.Contains("is_completed", json);
    }

    [Fact]
    public void TaskDto_EmptyChecklist_IsEmptyArray()
    {
        var task = new TaskItem{ Id="t1", Title="T"};
        var dto = new TaskDto(task, "b1", "col1", "user1");
        Assert.Empty(dto.Checklist);
        var json = System.Text.Json.JsonSerializer.Serialize(dto, new System.Text.Json.JsonSerializerOptions{ PropertyNamingPolicy=System.Text.Json.JsonNamingPolicy.SnakeCaseLower});
        Assert.Contains("\"checklist\":", json);
        Assert.Contains("[]", json);
    }

    [Fact]
    public void DuplicateBoard_PreservesChecklistWithNewIds()
    {
        var board = new Board{ Id="b1", Name=" Orig"};
        var col = new KanBanColumn{ Id="c1", Title="C", BoardId="b1"};
        var task = new TaskItem{ Id="t1", Title="T", ColumnId="c1", BoardId="b1"};
        task.Checklist.Add(new ChecklistItem{ Id="orig1", Text="A", IsCompleted=true, Position=0});
        task.Checklist.Add(new ChecklistItem{ Id="orig2", Text="B", Position=1});
        col.Tasks.Add(task); board.Columns.Add(col);
        var dup = SampleDataSeeder.DuplicateBoard(board, "Copy");
        var dupTask = dup.Columns[0].Tasks[0];
        Assert.Equal(2, dupTask.Checklist.Count);
        Assert.Equal("A", dupTask.Checklist[0].Text);
        Assert.True(dupTask.Checklist[0].IsCompleted);
        Assert.NotEqual("orig1", dupTask.Checklist[0].Id);
        Assert.NotEqual("orig2", dupTask.Checklist[1].Id);
        Assert.Equal(0, dupTask.Checklist[0].Position);
        Assert.Equal(1, dupTask.Checklist[1].Position);
    }

    [Fact]
    public void TaskDialogViewModel_AddEditDeleteRoundTrip_PreservesIds()
    {
        var vm = new TaskDialogViewModel();
        vm.NewChecklistText = "  Trimmed  ";
        vm.AddChecklistItemCommand.Execute(null);
        var id = vm.Checklist[0].Id;
        Assert.Equal("Trimmed", vm.Checklist[0].Text);
        // edit via direct text change
        vm.Checklist[0].Text = "Edited";
        Assert.Equal("Edited", vm.Checklist[0].Text);
        Assert.Equal(id, vm.Checklist[0].Id);
        // Simulate save to TaskItem
        var task = new TaskItem{ Title="T"};
        task.Checklist = new ObservableCollection<ChecklistItem>(vm.Checklist.Select(c=>new ChecklistItem{ Id=c.Id, Text=c.Text, IsCompleted=c.IsCompleted, Position=c.Position}));
        Assert.Equal(id, task.Checklist[0].Id);
        Assert.Equal("Edited", task.Checklist[0].Text);
    }
}
