using System;
using System.Threading.Tasks;
using KanTab.Services;
using KanTab.Storage;
using KanTab.Storage.Supabase;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace KanTab.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IAuthService? _authService;
    private readonly ICloudKanTabRepository? _cloudRepository;
    private readonly ISyncService? _syncService;
    private readonly WorkspaceState? _workspace;
    private readonly IFileDialogService? _fileDialog;

    [ObservableProperty]
    private string? _currentUserEmail;

    [ObservableProperty]
    private string _syncStatus = "Local only";

    [ObservableProperty]
    private bool _isSignedIn;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    // --- Notification preferences (bound to Settings UI) ---
    [ObservableProperty]
    private bool _notificationsEnabled;

    [ObservableProperty]
    private ReminderLeadTime _reminderLeadMinutes;

    [ObservableProperty]
    private bool _overdueNotificationsEnabled;

    public Array ReminderOptions { get; } = Enum.GetValues(typeof(ReminderLeadTime));

    public SettingsViewModel()
    {
        _authService = null;
        _cloudRepository = null;
        _syncService = null;
        CurrentUserEmail = null;
        IsSignedIn = false;
        _notificationsEnabled = true;
        _reminderLeadMinutes = ReminderLeadTime.Minutes15;
        _overdueNotificationsEnabled = true;
    }

    public SettingsViewModel(WorkspaceState? workspace, IFileDialogService? fileDialog)
    {
        _workspace = workspace;
        _fileDialog = fileDialog;
        if (workspace != null)
        {
            _notificationsEnabled = workspace.Settings.NotificationsEnabled;
            _reminderLeadMinutes = workspace.Settings.ReminderLeadMinutes;
            _overdueNotificationsEnabled = workspace.Settings.OverdueNotificationsEnabled;
        }
    }

    public SettingsViewModel(IAuthService authService, ICloudKanTabRepository cloudRepository, ISyncService syncService)
        : this(authService, cloudRepository, syncService, null, null)
    {
    }

    public SettingsViewModel(IAuthService authService, ICloudKanTabRepository cloudRepository, ISyncService syncService, WorkspaceState? workspace, IFileDialogService? fileDialog)
    {
        _authService = authService;
        _cloudRepository = cloudRepository;
        _syncService = syncService;
        _workspace = workspace;
        _fileDialog = fileDialog;

        if (workspace != null)
        {
            _notificationsEnabled = workspace.Settings.NotificationsEnabled;
            _reminderLeadMinutes = workspace.Settings.ReminderLeadMinutes;
            _overdueNotificationsEnabled = workspace.Settings.OverdueNotificationsEnabled;
        }

        if (_syncService != null)
            _syncService.StatusChanged += OnSyncStatusChanged;

        InitializeAuthState();
    }

    partial void OnNotificationsEnabledChanged(bool value) => PersistNotificationSettings();
    partial void OnReminderLeadMinutesChanged(ReminderLeadTime value) => PersistNotificationSettings();
    partial void OnOverdueNotificationsEnabledChanged(bool value) => PersistNotificationSettings();

    private void PersistNotificationSettings()
    {
        if (_workspace == null) return;
        _workspace.Settings.NotificationsEnabled = NotificationsEnabled;
        _workspace.Settings.ReminderLeadMinutes = ReminderLeadMinutes;
        _workspace.Settings.OverdueNotificationsEnabled = OverdueNotificationsEnabled;
        _workspace.MarkDirty();
    }

    public IRelayCommand SignOutCommand => new RelayCommand(async () => await SignOutAsyncImpl(), () => !IsLoading);
    public IRelayCommand RetrySyncCommand => new RelayCommand(() => _ = RetrySyncAsync(), () => !IsLoading);
    public IRelayCommand ManualSyncCommand => new RelayCommand(() => _ = TriggerManualSyncAsync(), () => !IsLoading);

    [RelayCommand]
    private async Task ExportBackup()
    {
        if (_workspace == null || _fileDialog == null)
        {
            ErrorMessage = "Export is not available in this context.";
            return;
        }
        try
        {
            var name = BackupService.SuggestedFileName();
            var path = await _fileDialog.ShowSaveDialogAsync(name, "Export Backup");
            if (string.IsNullOrEmpty(path)) return;
            var doc = BackupService.CreateBackup(_workspace);
            var json = BackupService.Serialize(doc);
            await _fileDialog.WriteTextAsync(path, json);
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Export failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ImportBackup()
    {
        if (_workspace == null || _fileDialog == null)
        {
            ErrorMessage = "Import is not available in this context.";
            return;
        }
        try
        {
            var path = await _fileDialog.ShowOpenDialogAsync("Import Backup");
            if (string.IsNullOrEmpty(path)) return;
            var json = await _fileDialog.ReadTextAsync(path);
            var result = BackupService.TryParseAndValidate(json, out var data, out var error);
            if (result != BackupImportResult.Ok || data == null)
            {
                ErrorMessage = error ?? "Invalid backup file.";
                return;
            }
            var confirmed = await _fileDialog.ShowConfirmAsync("Import Backup", "This will replace your current local KanTab data with the selected backup.\n\nContinue?");
            if (!confirmed) return;
            // Validate again after confirm in case file changed (paranoia), then apply atomically.
            var ok = BackupService.ApplyImport(_workspace, data);
            if (!ok)
            {
                ErrorMessage = _workspace.StorageError ?? "Import succeeded but saving failed. Your data is still in memory.";
                return;
            }
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Import failed: {ex.Message}";
        }
    }

    private void OnSyncStatusChanged(object? sender, SyncStatusChangedEventArgs e)
    {
        SyncStatus = e.Status.ToString();
        if (!string.IsNullOrEmpty(e.Message))
        {
            ErrorMessage = e.Message;
        }
    }

    /// <summary>Called by tests or host to simulate an auth refresh failure without real HTTP.</summary>
    public void HandleAuthRefreshFailed()
    {
        CurrentUserEmail = null;
        IsSignedIn = false;
        SyncStatus = "Local only";
        ErrorMessage = "Session expired. Please sign in again.";
    }

    private async void InitializeAuthState()
    {
        if (_authService == null) return;

        IsLoading = true;
        try
        {
            var user = await _authService.RestoreSessionAsync().ConfigureAwait(false);
            if (user != null)
            {
                CurrentUserEmail = user.Email;
                IsSignedIn = true;

                if (_syncService != null)
                {
                    await _syncService.StartAsync(user.Id).ConfigureAwait(false);
                }
                else
                {
                    SyncStatus = "Synced";
                }
            }
            else
            {
                CurrentUserEmail = null;
                IsSignedIn = false;
                SyncStatus = "Local only";
            }
        }
        catch
        {
            SyncStatus = "Local only";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SignOutAsyncImpl()
    {
        if (_authService == null) return;

        IsLoading = true;
        try
        {
            if (_syncService != null)
            {
                await _syncService.StopAsync().ConfigureAwait(false);
            }

            await _authService.SignOutAsync().ConfigureAwait(false);
            CurrentUserEmail = null;
            IsSignedIn = false;
            SyncStatus = "Local only";
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Sign out failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RetrySyncAsync()
    {
        if (_syncService == null || _authService?.CurrentUser == null) return;

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            await _syncService.StartAsync(_authService.CurrentUser.Id).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Sync retry failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task TriggerManualSyncAsync()
    {
        if (_syncService == null || _authService?.CurrentUser == null) return;

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            await _syncService.StartAsync(_authService.CurrentUser.Id).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Manual sync failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
