#pragma warning disable xUnit1031
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KanTab.Models;
using KanTab.Storage;
using KanTab.ViewModels;
using Xunit;

namespace KanTab.Tests;

public class BackupTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "KanTab.Tests", "Backup", Guid.NewGuid().ToString());
    private string FilePath => Path.Combine(_dir, "kantab.json");
    public BackupTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }
    private LocalJsonKanTabRepository Repo() => new(FilePath);

    private static Board MakeBoard(string name, int pos, string? id = null)
    {
        var b = new Board { Id = id ?? Guid.NewGuid().ToString(), Name = name, Position = pos, CreatedAt = new DateTime(2024,1,1), UpdatedAt = new DateTime(2024,1,2) };
        return b;
    }

    private static KanBanColumn MakeCol(Board b, string title, int pos, string? id = null)
        => new() { Id = id ?? Guid.NewGuid().ToString(), Title = title, BoardId = b.Id, Position = pos, CreatedAt = b.CreatedAt, UpdatedAt = b.UpdatedAt };

    private static TaskItem MakeTask(Board b, KanBanColumn col, string title, int pos, bool completed = false, DateOnly? due = null, Priority pri = Priority.Medium, string? desc = null, string? id = null)
        => new TaskItem { Id = id ?? Guid.NewGuid().ToString(), Title = title, Description = desc ?? string.Empty, Priority = pri, DueDate = due, IsCompleted = completed, BoardId = b.Id, ColumnId = col.Id, Position = pos, Tags = new List<string>(), CreatedAt = b.CreatedAt, UpdatedAt = b.UpdatedAt };

    private WorkspaceState SeededState()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        state.Boards.Clear(); state.Notes.Clear();
        var b = MakeBoard("B1", 0, "b1");
        var c = MakeCol(b, "To Do", 0, "c1");
        b.Columns.Add(c);
        var t = MakeTask(b, c, "Task1", 0, completed: true, due: new DateOnly(2026,9,10), pri: Priority.High, desc: "Desc", id: "t1");
        t.Tags.AddRange(new[] { "tagA", "tagB" });
        t.Checklist.Add(new ChecklistItem { Id = "cl1", Text = "Item 1", IsCompleted = true, Position = 0, CreatedAt = b.CreatedAt, UpdatedAt = b.UpdatedAt });
        t.Checklist.Add(new ChecklistItem { Id = "cl2", Text = "Item 2", IsCompleted = false, Position = 1, CreatedAt = b.CreatedAt, UpdatedAt = b.UpdatedAt });
        t.Checklist.Add(new ChecklistItem { Id = "cl3", Text = "Item 3", IsCompleted = false, Position = 2, CreatedAt = b.CreatedAt, UpdatedAt = b.UpdatedAt });
        c.Tasks.Add(t);
        var t2 = MakeTask(b, c, "Task2", 1, id: "t2");
        c.Tasks.Add(t2);
        state.Boards.Add(b);
        state.SelectedBoard = b;
        var note = new Note { Id = "n1", Title = "Note1", Content = "Hello", IsPinned = true, Position = 0, CreatedAt = b.CreatedAt, UpdatedAt = b.UpdatedAt };
        var note2 = new Note { Id = "n2", Title = "Note2", Content = "World", IsPinned = false, Position = 1, CreatedAt = b.CreatedAt, UpdatedAt = b.UpdatedAt };
        state.Notes.Add(note); state.Notes.Add(note2);
        state.SaveNow();
        return state;
    }

    // ---- Export ----
    [Fact]
    public void Export_ContainsBoardsTasksChecklistNotes()
    {
        var state = SeededState();
        var doc = BackupService.CreateBackup(state);
        Assert.Equal(1, doc.FormatVersion);
        Assert.True(doc.ExportedAt > DateTime.MinValue);
        Assert.Single(doc.Data.Boards);
        Assert.Equal(2, doc.Data.Boards[0].Columns[0].Tasks.Count);
        Assert.Equal(3, doc.Data.Boards[0].Columns[0].Tasks[0].Checklist.Count);
        Assert.Equal(2, doc.Data.Notes.Count);
    }

    [Fact]
    public void Export_OrderingPreserved()
    {
        var state = SeededState();
        var b2 = MakeBoard("B2", 1, "b2");
        var c2 = MakeCol(b2, "Col", 0, "c2");
        b2.Columns.Add(c2);
        state.Boards.Add(b2);
        var doc = BackupService.CreateBackup(state);
        Assert.Equal(new[] { "b1", "b2" }, doc.Data.Boards.Select(b => b.Id));
        Assert.Equal(new[] { "n1", "n2" }, doc.Data.Notes.Select(n => n.Id));
        var tasks = doc.Data.Boards[0].Columns[0].Tasks;
        Assert.Equal(new[] { "t1", "t2" }, tasks.Select(t => t.Id));
        Assert.Equal(new[] { "cl1", "cl2", "cl3" }, tasks[0].Checklist.Select(c => c.Id));
    }

    [Fact]
    public void Export_CompletionAndPinnedPreserved()
    {
        var state = SeededState();
        var doc = BackupService.CreateBackup(state);
        Assert.True(doc.Data.Boards[0].Columns[0].Tasks[0].IsCompleted);
        Assert.True(doc.Data.Boards[0].Columns[0].Tasks[0].Checklist[0].IsCompleted);
        Assert.False(doc.Data.Boards[0].Columns[0].Tasks[0].Checklist[1].IsCompleted);
        Assert.True(doc.Data.Notes[0].IsPinned);
        Assert.False(doc.Data.Notes[1].IsPinned);
    }

    [Fact]
    public void Export_SecretsNotIncluded()
    {
        var state = SeededState();
        var doc = BackupService.CreateBackup(state);
        var json = BackupService.Serialize(doc).ToLowerInvariant();
        Assert.DoesNotContain("password", json);
        Assert.DoesNotContain("token", json);
        Assert.DoesNotContain("apikey", json);
        Assert.DoesNotContain("secret", json);
        Assert.DoesNotContain("supabase", json);
    }

    [Fact]
    public void Export_SuggestedFileNameFormat()
    {
        var name = BackupService.SuggestedFileName(new DateTime(2026,9,7));
        Assert.Equal("KanTab-Backup-2026-09-07.json", name);
    }

    // ---- Import ----
    [Fact]
    public void Import_ValidBackup_ReplacesData()
    {
        var state = SeededState();
        var json = BackupService.Serialize(BackupService.CreateBackup(state));
        // mutate state to something else
        state.Boards.Clear();
        state.Boards.Add(MakeBoard("Other", 0, "other"));
        state.Notes.Clear();
        Assert.Single(state.Boards);
        // import
        var res = BackupService.TryParseAndValidate(json, out var data, out var err);
        Assert.Equal(BackupImportResult.Ok, res);
        Assert.NotNull(data);
        BackupService.ApplyImport(state, data!);
        Assert.Single(state.Boards);
        Assert.Equal("b1", state.Boards[0].Id);
        Assert.Equal(2, state.Notes.Count);
    }

    [Fact]
    public void Import_InvalidJson_Rejected_NoMutation()
    {
        var state = SeededState();
        var before = state.Boards[0].Name;
        var res = BackupService.TryParseAndValidate("{ not json", out var data, out var err);
        Assert.Equal(BackupImportResult.InvalidJson, res);
        Assert.Null(data);
        Assert.NotNull(err);
        Assert.Equal(before, state.Boards[0].Name);
        Assert.Equal(2, state.Boards[0].Columns[0].Tasks.Count);
    }

    [Fact]
    public void Import_UnsupportedVersion_Rejected()
    {
        var state = SeededState();
        var doc = BackupService.CreateBackup(state);
        doc.FormatVersion = 999;
        var json = BackupService.Serialize(doc);
        var res = BackupService.TryParseAndValidate(json, out var data, out var err);
        Assert.Equal(BackupImportResult.UnsupportedVersion, res);
        Assert.Null(data);
    }

    [Fact]
    public void Import_DuplicateIds_Sanitized()
    {
        var state = SeededState();
        var doc = BackupService.CreateBackup(state);
        // duplicate board ids, task ids, checklist ids
        doc.Data.Boards.Add(doc.Data.Boards[0]);
        doc.Data.Boards[0].Columns[0].Tasks.Add(doc.Data.Boards[0].Columns[0].Tasks[0]);
        doc.Data.Notes.Add(doc.Data.Notes[0]);
        var json = BackupService.Serialize(doc);
        var res = BackupService.TryParseAndValidate(json, out var data, out var err);
        Assert.Equal(BackupImportResult.Ok, res);
        Assert.NotNull(data);
        // ids must be distinct per-scope after sanitize (boards global, tasks per-column, notes global)
        var bids = data!.Boards.Select(b => b.Id).ToList();
        Assert.Equal(bids.Distinct().Count(), bids.Count);
        // tasks are scoped per column in Sanitize — verify per-column distinctness
        foreach (var board in data.Boards)
            foreach (var col in board.Columns)
            {
                var tids = col.Tasks.Select(t => t.Id).ToList();
                Assert.Equal(tids.Distinct().Count(), tids.Count);
            }
        var nids = data.Notes.Select(n => n.Id).ToList();
        Assert.Equal(nids.Distinct().Count(), nids.Count);
    }

    [Fact]
    public void Import_MissingFields_Handled()
    {
        var json = "{\"formatVersion\":1,\"exportedAt\":\"2026-09-07T00:00:00Z\",\"data\":{\"boards\":null,\"notes\":null}}";
        var res = BackupService.TryParseAndValidate(json, out var data, out var err);
        Assert.Equal(BackupImportResult.Ok, res);
        Assert.NotNull(data);
        Assert.Empty(data!.Boards);
        Assert.Empty(data!.Notes);
    }

    [Fact]
    public void Import_ChecklistSurvivesRoundTrip()
    {
        var state = SeededState();
        var json = BackupService.Serialize(BackupService.CreateBackup(state));
        var res = BackupService.TryParseAndValidate(json, out var data, out _);
        Assert.Equal(BackupImportResult.Ok, res);
        var cl = data!.Boards[0].Columns[0].Tasks[0].Checklist;
        Assert.Equal(3, cl.Count);
        Assert.Equal(new[] { 0,1,2 }, cl.Select(c => c.Position));
        Assert.Equal(new[] { true,false,false }, cl.Select(c => c.IsCompleted));
        Assert.Equal(new[] { "cl1","cl2","cl3" }, cl.Select(c => c.Id));
    }

    [Fact]
    public void Import_ReplaceNotMerge()
    {
        var state = SeededState();
        var json = BackupService.Serialize(BackupService.CreateBackup(state));
        // state currently has b1; after import it should still only have b1, not b1 + something
        state.Boards.Add(MakeBoard("Extra", 99, "extra"));
        Assert.Equal(2, state.Boards.Count);
        BackupService.TryParseAndValidate(json, out var data, out _);
        BackupService.ApplyImport(state, data!);
        Assert.DoesNotContain(state.Boards, b => b.Id == "extra");
        Assert.Single(state.Boards);
    }

    [Fact]
    public void Import_PersistsAtomically()
    {
        var state = SeededState();
        var json = BackupService.Serialize(BackupService.CreateBackup(state));
        BackupService.TryParseAndValidate(json, out var data, out _);
        // Apply to a new repo
        var dir2 = Path.Combine(Path.GetTempPath(), "KanTab.Tests", "BackupPersist", Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir2);
        try
        {
            var repo2 = new LocalJsonKanTabRepository(Path.Combine(dir2, "kantab.json"));
            var state2 = WorkspaceState.Load(repo2, useDispatcherTimer: false);
            BackupService.ApplyImport(state2, data!);
            var loaded = repo2.LoadAsync().GetAwaiter().GetResult();
            Assert.Single(loaded.Boards);
            Assert.Equal(3, loaded.Boards[0].Columns[0].Tasks[0].Checklist.Count);
            Assert.Equal(2, loaded.Notes.Count);
        }
        finally { try { Directory.Delete(dir2, true); } catch { } }
    }

    [Fact]
    public void Import_InvalidDoesNotDestroyWorkspace()
    {
        var state = SeededState();
        var snapshot = BackupService.Serialize(BackupService.CreateBackup(state));
        var res = BackupService.TryParseAndValidate("not json at all", out var data, out _);
        Assert.Equal(BackupImportResult.InvalidJson, res);
        Assert.Null(data);
        // workspace untouched
        Assert.Single(state.Boards);
        Assert.Equal(2, state.Boards[0].Columns[0].Tasks.Count);
        // file untouched
        var repo = new LocalJsonKanTabRepository(FilePath);
        var loaded = repo.LoadAsync().GetAwaiter().GetResult();
        Assert.Single(loaded.Boards);
    }

    [Fact]
    public void RoundTrip_FullWorkspace()
    {
        var state = SeededState();
        var json = BackupService.Serialize(BackupService.CreateBackup(state));
        // new clean workspace
        var dir2 = Path.Combine(Path.GetTempPath(), "KanTab.Tests", "BackupRT", Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir2);
        try
        {
            var repo2 = new LocalJsonKanTabRepository(Path.Combine(dir2, "kantab.json"));
            var state2 = WorkspaceState.Load(repo2, useDispatcherTimer: false);
            state2.Boards.Clear(); state2.Notes.Clear();
            BackupService.TryParseAndValidate(json, out var data, out _);
            BackupService.ApplyImport(state2, data!);
            Assert.Single(state2.Boards);
            Assert.Equal("B1", state2.Boards[0].Name);
            Assert.Single(state2.Boards[0].Columns);
            Assert.Equal(2, state2.Boards[0].Columns[0].Tasks.Count);
            var t = state2.Boards[0].Columns[0].Tasks[0];
            Assert.Equal("Desc", t.Description);
            Assert.Equal(Priority.High, t.Priority);
            Assert.Equal(new DateOnly(2026,9,10), t.DueDate);
            Assert.Equal(3, t.Checklist.Count);
            Assert.Equal("Item 1", t.Checklist[0].Text);
            Assert.True(t.Checklist[0].IsCompleted);
            Assert.Equal(2, state2.Notes.Count);
            Assert.True(state2.Notes.Any(n => n.IsPinned));
        }
        finally { try { Directory.Delete(dir2, true); } catch { } }
    }
}
