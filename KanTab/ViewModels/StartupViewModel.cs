using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KanTab.Storage;
using KanTab.Storage.Supabase;

namespace KanTab.ViewModels;

/// <summary>
/// Startup phase exposed to the loading screen. Drives status text + animation state.
/// No artificial delays — each phase awaits real work.
/// </summary>
public enum StartupPhase { Starting, LoadingWorkspace, RestoringSession, StartingSync, Ready, OfflineReady, Failed }

public partial class StartupViewModel : ViewModelBase
{
    [ObservableProperty] private StartupPhase _phase = StartupPhase.Starting;
    [ObservableProperty] private string _statusText = "Starting KanTab…";
    [ObservableProperty] private bool _isBusy = true;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _canRetry;
    [ObservableProperty] private bool _canContinueOffline;
    [ObservableProperty] private bool _isComplete;

    private Func<CancellationToken, Task<bool>>? _retryAction;
    private Func<CancellationToken, Task>? _continueOfflineAction;

    public void SetRetry(Func<CancellationToken, Task<bool>> retry, Func<CancellationToken, Task>? continueOffline = null)
    {
        _retryAction = retry;
        _continueOfflineAction = continueOffline;
    }

    public void SetPhase(StartupPhase phase, string? detail = null)
    {
        Phase = phase;
        IsBusy = phase is not (StartupPhase.Ready or StartupPhase.Failed or StartupPhase.OfflineReady);
        IsComplete = phase is StartupPhase.Ready or StartupPhase.OfflineReady;
        StatusText = phase switch
        {
            StartupPhase.Starting => "Starting KanTab…",
            StartupPhase.LoadingWorkspace => "Loading workspace…",
            StartupPhase.RestoringSession => "Restoring session…",
            StartupPhase.StartingSync => "Starting synchronization…",
            StartupPhase.Ready => "Ready",
            StartupPhase.OfflineReady => detail ?? "Offline — using local data",
            StartupPhase.Failed => detail ?? "Something went wrong",
            _ => StatusText
        };
        if (phase == StartupPhase.Failed)
        {
            ErrorMessage = detail;
            CanRetry = _retryAction != null;
            CanContinueOffline = _continueOfflineAction != null;
        }
        else
        {
            ErrorMessage = null;
        }
    }

    [RelayCommand]
    private async Task RetryAsync(CancellationToken ct)
    {
        if (_retryAction == null) return;
        CanRetry = false;
        ErrorMessage = null;
        SetPhase(StartupPhase.Starting);
        var ok = await _retryAction(ct).ConfigureAwait(false);
        if (!ok && Phase != StartupPhase.Ready && Phase != StartupPhase.OfflineReady)
            CanRetry = true;
    }

    [RelayCommand]
    private async Task ContinueOfflineAsync(CancellationToken ct)
    {
        if (_continueOfflineAction == null) return;
        await _continueOfflineAction(ct).ConfigureAwait(false);
    }
}
