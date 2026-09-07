using KanTab.Models;
using KanTab.ViewModels;

namespace KanTab.Tests;

public class NotesLogicTests
{
    private static Note Make(string title, bool pinned, DateTime updated) =>
        new()
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            Content = "some content",
            IsPinned = pinned,
            UpdatedAt = updated,
            CreatedAt = updated
        };

    [Fact]
    public void OrderNotes_PinnedComeFirst_ThenByMostRecent()
    {
        var older = Make("older pinned", true, new DateTime(2024, 1, 1));
        var newerUnpinned = Make("newer unpinned", false, new DateTime(2024, 5, 1));
        var newerPinned = Make("newer pinned", true, new DateTime(2024, 3, 1));
        var olderUnpinned = Make("older unpinned", false, new DateTime(2024, 2, 1));

        var ordered = NotesViewModel.OrderNotes(new[] { older, newerUnpinned, newerPinned, olderUnpinned }).ToList();

        // All pinned first (ordered by newest UpdatedAt), then unpinned (newest first).
        Assert.Equal(
            new[] { "newer pinned", "older pinned", "newer unpinned", "older unpinned" },
            ordered.Select(n => n.Title));
    }

    [Fact]
    public void FilterNotes_EmptySearch_ReturnsAll()
    {
        var notes = new[] { Make("Alpha", false, DateTime.Now), Make("Beta", false, DateTime.Now) };

        var result = NotesViewModel.FilterNotes(notes, string.Empty).ToList();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void FilterNotes_MatchesTitleCaseInsensitive()
    {
        var notes = new[] { Make("Shopping list", false, DateTime.Now), Make("Daily notes", false, DateTime.Now) };

        var result = NotesViewModel.FilterNotes(notes, "SHOPPING").ToList();

        Assert.Single(result);
        Assert.Equal("Shopping list", result[0].Title);
    }

    [Fact]
    public void FilterNotes_MatchesContentCaseInsensitive()
    {
        var note = Make("Anything", false, DateTime.Now);
        note.Content = "Remember to buy milk";
        var other = Make("Other", false, DateTime.Now);

        var result = NotesViewModel.FilterNotes(new[] { note, other }, "MILK").ToList();

        Assert.Single(result);
        Assert.Same(note, result[0]);
    }

    [Fact]
    public void FilterNotes_NoMatchesReturnsEmpty()
    {
        var notes = new[] { Make("Alpha", false, DateTime.Now) };

        var result = NotesViewModel.FilterNotes(notes, "zzzzz").ToList();

        Assert.Empty(result);
    }
}

public class NoteListPreviewTests
{
    [Fact]
    public void ListItem_BlankTitleUsesUntitledFallback()
    {
        var item = new NoteListItemViewModel(new Note { Title = "   " }, false);

        Assert.Equal("Untitled note", item.Title);
    }

    [Fact]
    public void ListItem_PreviewFlattensWhitespaceAndTruncates()
    {
        var note = new Note { Title = "T", Content = "Line one\n\nLine two" + new string('x', 200) };
        var item = new NoteListItemViewModel(note, false);

        Assert.DoesNotContain("\n", item.Preview);
        Assert.Equal(97, item.Preview.Length); // 96 chars + ellipsis
        Assert.EndsWith("…", item.Preview);
    }

    [Fact]
    public void ListItem_ShortContentIsShownWithoutTruncation()
    {
        var item = new NoteListItemViewModel(new Note { Title = "T", Content = "hello world" }, false);

        Assert.Equal("hello world", item.Preview);
    }

    [Fact]
    public void ListItem_ReflectsPinnedState()
    {
        var item = new NoteListItemViewModel(new Note { IsPinned = true }, false);

        Assert.True(item.IsPinned);
    }

    [Fact]
    public void ListItem_IsSelectedReflectsConstructorArgument()
    {
        var selected = new NoteListItemViewModel(new Note(), true);
        var unselected = new NoteListItemViewModel(new Note(), false);

        Assert.True(selected.IsSelected);
        Assert.False(unselected.IsSelected);
    }
}

public class NoteModelTests
{
    [Fact]
    public void Note_DefaultsToUntitledAndGeneratedId()
    {
        var note = new Note();

        Assert.Equal("Untitled note", note.Title);
        Assert.False(string.IsNullOrWhiteSpace(note.Id));
        Assert.False(note.IsPinned);
        Assert.Equal(string.Empty, note.Content);
    }

    [Fact]
    public void Note_CreatedAndUpdatedAtAreSetByDefault()
    {
        var note = new Note();

        Assert.NotEqual(default, note.CreatedAt);
        Assert.NotEqual(default, note.UpdatedAt);
    }
}