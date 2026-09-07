using System.Collections.Generic;
using System.ComponentModel;
using KanTab.ViewModels;
using Xunit;

namespace KanTab.Tests;

public class TaskDialogViewModelTests
{
    [Fact]
    public void NewTaskDialog_PrimaryActionText_IsCreate()
    {
        var vm = new TaskDialogViewModel();

        Assert.False(vm.IsEditMode);
        Assert.Equal("Create", vm.PrimaryActionText);
    }

    [Fact]
    public void EditTaskDialog_PrimaryActionText_IsSave()
    {
        var vm = new TaskDialogViewModel
        {
            IsEditMode = true
        };

        Assert.True(vm.IsEditMode);
        Assert.Equal("Save", vm.PrimaryActionText);
    }

    [Fact]
    public void Switching_FromCreateToEdit_UpdatesToSave()
    {
        var vm = new TaskDialogViewModel();

        Assert.Equal("Create", vm.PrimaryActionText);

        vm.IsEditMode = true;

        Assert.True(vm.IsEditMode);
        Assert.Equal("Save", vm.PrimaryActionText);
    }

    [Fact]
    public void Switching_FromEditToCreate_UpdatesToCreate()
    {
        var vm = new TaskDialogViewModel { IsEditMode = true };

        Assert.Equal("Save", vm.PrimaryActionText);

        vm.IsEditMode = false;

        Assert.False(vm.IsEditMode);
        Assert.Equal("Create", vm.PrimaryActionText);
    }

    [Fact]
    public void Switching_MultipleTimes_CorrectlyUpdatesState()
    {
        var vm = new TaskDialogViewModel();

        Assert.Equal("Create", vm.PrimaryActionText);

        vm.IsEditMode = true;
        Assert.Equal("Save", vm.PrimaryActionText);

        vm.IsEditMode = false;
        Assert.Equal("Create", vm.PrimaryActionText);

        vm.IsEditMode = true;
        Assert.Equal("Save", vm.PrimaryActionText);

        vm.IsEditMode = false;
        Assert.Equal("Create", vm.PrimaryActionText);
    }

    [Fact]
    public void CancellingEditAndCreatingNew_AfterEditResetsToCreate()
    {
        var editVm = new TaskDialogViewModel { IsEditMode = true };
        Assert.Equal("Save", editVm.PrimaryActionText);

        var newVm = new TaskDialogViewModel { IsEditMode = false };
        Assert.Equal("Create", newVm.PrimaryActionText);

        var reusedVm = new TaskDialogViewModel { IsEditMode = true };
        reusedVm.IsEditMode = false;
        Assert.False(reusedVm.IsEditMode);
        Assert.Equal("Create", reusedVm.PrimaryActionText);
    }

    [Fact]
    public void OpeningDifferentTasksForEditing_AlwaysShowsSave()
    {
        var vm = new TaskDialogViewModel { IsEditMode = true, Title = "Task A" };
        Assert.Equal("Save", vm.PrimaryActionText);

        vm.Title = "Task B";
        Assert.True(vm.IsEditMode);
        Assert.Equal("Save", vm.PrimaryActionText);

        vm.Title = "Task C";
        vm.IsEditMode = true;
        Assert.Equal("Save", vm.PrimaryActionText);

        vm.IsEditMode = false;
        Assert.Equal("Create", vm.PrimaryActionText);
        vm.IsEditMode = true;
        vm.Title = "Task D";
        Assert.Equal("Save", vm.PrimaryActionText);
    }

    [Fact]
    public void IsEditMode_Change_RaisesPropertyChanged_ForPrimaryActionText()
    {
        var vm = new TaskDialogViewModel();
        var changed = new List<string?>();
        vm.PropertyChanged += (s, e) => changed.Add(e.PropertyName);

        vm.IsEditMode = true;

        Assert.Contains(nameof(TaskDialogViewModel.PrimaryActionText), changed);
        Assert.Contains(nameof(TaskDialogViewModel.IsEditMode), changed);
    }

    [Fact]
    public void IsEditMode_Change_RaisesPrimaryActionText_EachToggle()
    {
        var vm = new TaskDialogViewModel();
        int primaryChanges = 0;
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(TaskDialogViewModel.PrimaryActionText))
                primaryChanges++;
        };

        vm.IsEditMode = true;
        vm.IsEditMode = false;
        vm.IsEditMode = true;
        vm.IsEditMode = false;

        Assert.Equal(4, primaryChanges);
    }

    [Fact]
    public void IsEditMode_SameValue_DoesNotRaisePrimaryActionText()
    {
        var vm = new TaskDialogViewModel();
        int primaryChanges = 0;
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(TaskDialogViewModel.PrimaryActionText))
                primaryChanges++;
        };

        vm.IsEditMode = false;
        Assert.Equal(0, primaryChanges);

        vm.IsEditMode = true;
        Assert.Equal(1, primaryChanges);

        vm.IsEditMode = true;
        Assert.Equal(1, primaryChanges);
    }
}
