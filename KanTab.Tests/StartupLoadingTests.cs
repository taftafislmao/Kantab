using KanTab.ViewModels;
using Xunit;

namespace KanTab.Tests;

public class StartupLoadingTests
{
    [Fact]
    public void Loading_StartsInStartingPhase()
    {
        var vm = new StartupViewModel();
        Assert.Equal(StartupPhase.Starting, vm.Phase);
        Assert.True(vm.IsBusy);
        Assert.False(vm.IsComplete);
        Assert.Contains("Starting", vm.StatusText);
    }

    [Fact]
    public void Loading_PhasesAdvance()
    {
        var vm = new StartupViewModel();
        vm.SetPhase(StartupPhase.LoadingWorkspace);
        Assert.Equal(StartupPhase.LoadingWorkspace, vm.Phase);
        Assert.True(vm.IsBusy);
        vm.SetPhase(StartupPhase.RestoringSession);
        Assert.Equal(StartupPhase.RestoringSession, vm.Phase);
        vm.SetPhase(StartupPhase.StartingSync);
        Assert.Equal("Starting synchronization…", vm.StatusText);
    }

    [Fact]
    public void Loading_ReadyCompletes()
    {
        var vm = new StartupViewModel();
        vm.SetPhase(StartupPhase.Ready);
        Assert.True(vm.IsComplete);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public void Loading_OfflineReadyIsCompleteWithCustomText()
    {
        var vm = new StartupViewModel();
        vm.SetPhase(StartupPhase.OfflineReady, "Offline — using local data");
        Assert.True(vm.IsComplete);
        Assert.Equal("Offline — using local data", vm.StatusText);
    }

    [Fact]
    public void Loading_FailedShowsErrorAndRetry()
    {
        var vm = new StartupViewModel();
        vm.SetRetry(_ => System.Threading.Tasks.Task.FromResult(true));
        vm.SetPhase(StartupPhase.Failed, "Cloud unreachable");
        Assert.Equal(StartupPhase.Failed, vm.Phase);
        Assert.True(vm.CanRetry);
        Assert.Equal("Cloud unreachable", vm.ErrorMessage);
        Assert.False(vm.IsComplete);
    }

    [Fact]
    public void Loading_EveryLaunchStartsFresh()
    {
        var vm1 = new StartupViewModel();
        vm1.SetPhase(StartupPhase.Ready);
        var vm2 = new StartupViewModel();
        Assert.Equal(StartupPhase.Starting, vm2.Phase);
        Assert.False(vm2.IsComplete);
    }

    [Fact]
    public void Loading_RetryAndContinueOfflineActions()
    {
        var vm = new StartupViewModel();
        bool retried = false, continued = false;
        vm.SetRetry(_ => { retried = true; return System.Threading.Tasks.Task.FromResult(true); },
            _ => { continued = true; return System.Threading.Tasks.Task.CompletedTask; });
        vm.SetPhase(StartupPhase.Failed, "fail");
        Assert.True(vm.CanContinueOffline);
    }
}
