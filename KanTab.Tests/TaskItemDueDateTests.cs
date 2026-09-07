using KanTab.Models;
using Xunit;

namespace KanTab.Tests;

public class TaskItemDueDateTests
{
    [Fact]
    public void TaskItem_DueDateRelativeDisplay_ShowsCorrectText()
    {
        var today = DateTime.Today;
        var task = new TaskItem { Title = "Test", DueDate = DateOnly.FromDateTime(today) };

        Assert.Equal("Today", task.DueDateRelativeDisplay);
    }

    [Fact]
    public void TaskItem_DueDateRelativeDisplay_Overdue()
    {
        var task = new TaskItem { Title = "Test", DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-1)) };

        Assert.Equal("Overdue", task.DueDateRelativeDisplay);
    }

    [Fact]
    public void TaskItem_DueDateRelativeDisplay_NoDueDate()
    {
        var task = new TaskItem { Title = "Test" };

        Assert.Equal("No due date", task.DueDateRelativeDisplay);
    }

    [Fact]
    public void TaskItem_IsOverdue_TrueWhenPastDue()
    {
        var task = new TaskItem
        {
            Title = "Test",
            DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-1))
        };

        Assert.True(task.IsOverdue);
    }

    [Fact]
    public void TaskItem_IsOverdue_FalseWhenCompleted()
    {
        var task = new TaskItem
        {
            Title = "Test",
            DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-1)),
            IsCompleted = true
        };

        Assert.False(task.IsOverdue);
    }

    [Fact]
    public void TaskItem_IsOverdue_FalseWhenFutureDate()
    {
        var task = new TaskItem
        {
            Title = "Test",
            DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(5))
        };

        Assert.False(task.IsOverdue);
    }

    [Fact]
    public void TaskItem_IsDueToday_TrueOnDueDate()
    {
        var task = new TaskItem
        {
            Title = "Test",
            DueDate = DateOnly.FromDateTime(DateTime.Today)
        };

        Assert.True(task.IsDueToday);
    }

    [Fact]
    public void TaskItem_IsDueToday_FalseWhenCompleted()
    {
        var task = new TaskItem
        {
            Title = "Test",
            DueDate = DateOnly.FromDateTime(DateTime.Today),
            IsCompleted = true
        };

        Assert.False(task.IsDueToday);
    }

    [Fact]
    public void TaskItem_DueDateDisplay_FormatsShort()
    {
        var task = new TaskItem { Title = "Test", DueDate = new DateOnly(2026, 9, 5) };

        var display = task.DueDateDisplay;
        Assert.NotNull(display);
        Assert.Contains("Sep", display);
        Assert.Contains("5", display);
    }

    [Fact]
    public void TaskItem_DueDateRelativeDisplay_ThisWeek()
    {
        var task = new TaskItem
        {
            Title = "Test",
            DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(3))
        };

        Assert.Contains("This week", task.DueDateRelativeDisplay);
    }

    [Fact]
    public void TaskItem_DueDateRelativeDisplay_FutureDate()
    {
        var task = new TaskItem
        {
            Title = "Test",
            DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(10))
        };

        var display = task.DueDateRelativeDisplay;
        Assert.NotNull(display);
    }
}
