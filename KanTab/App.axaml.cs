using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using KanTab.Services;
using KanTab.Storage;
using KanTab.Storage.Supabase;
using KanTab.ViewModels;
using KanTab.Views;
using KanTab.Themes;
using System;
using System.IO;

namespace KanTab;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Resources.MergedDictionaries.Add(new Theme());

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var startupVm = new StartupViewModel();
            var startupView = new StartupView { DataContext = startupVm };

            var mainWindow = new MainWindow();
            var originalShellContent = mainWindow.Content as Control;
            mainWindow.Content = null;

            var root = new ContentControl { Content = startupView };
            mainWindow.Content = root;
            LogStartup("MainWindow created. Will run startup. exe=" + System.Environment.ProcessPath);
            desktop.MainWindow = mainWindow;

            var cts = new System.Threading.CancellationTokenSource();
            desktop.ShutdownRequested += (_, _) => { try { cts.Cancel(); } catch { } };

            _ = RunStartupAsync(startupVm, root, originalShellContent, mainWindow, desktop, cts.Token);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async System.Threading.Tasks.Task RunStartupAsync(
        StartupViewModel startupVm,
        ContentControl root,
        Control? originalShellContent,
        MainWindow mainWindow,
        IClassicDesktopStyleApplicationLifetime desktop,
        System.Threading.CancellationToken ct)
    {
        WorkspaceState? state = null;
        ISyncService? syncService = null;
        NotificationScheduler? scheduler = null;
        MainWindowViewModel? mainVm = null;

        await Dispatcher.UIThread.InvokeAsync(() => { LogStartup("First render pumped."); }, DispatcherPriority.Render);

        async System.Threading.Tasks.Task<bool> DoInit()
        {
            SupabaseConfig config = default;
            try
            {
                config = SupabaseConfig.Load();
                LogStartup($"[STARTUP] Starting. IsConfigured={config.IsConfigured}");
                startupVm.SetPhase(StartupPhase.Starting);

                IKanTabRepository localRepository = new LocalJsonKanTabRepository();
                ICloudKanTabRepository? cloudRepository = null;
                IAuthService? authService = null;

                if (config.IsConfigured)
                {
                    authService = new SupabaseAuthService(config);
                    cloudRepository = new SupabaseKanTabRepository(config);
                    syncService = new SyncService(config, cloudRepository!, null, () => authService?.GetCurrentAccessToken());
                }

                startupVm.SetPhase(StartupPhase.LoadingWorkspace);
                state = WorkspaceState.Load(localRepository);
                LogStartup($"[STARTUP] Workspace loaded. InitialSetupCompleted={state.Settings.InitialSetupCompleted}, Boards={state.Boards.Count}");
                if (ct.IsCancellationRequested) return false;

                AuthUser? restoredUser = null;
                if (authService != null)
                {
                    startupVm.SetPhase(StartupPhase.RestoringSession);
                    try { restoredUser = await authService.RestoreSessionAsync().ConfigureAwait(false); } catch (System.Exception ex) { LogStartup($"[STARTUP] RestoreSession error: {ex}"); }
                    LogStartup($"[STARTUP] RestoredUser={(restoredUser==null?"null":restoredUser.Email)}");
                    if (ct.IsCancellationRequested) return false;
                    if (restoredUser != null && cloudRepository != null)
                    {
                        startupVm.SetPhase(StartupPhase.StartingSync);
                        try
                        {
                            var cloudData = await cloudRepository.LoadAsync(restoredUser.Id).ConfigureAwait(false);
                            if (cloudData.Boards.Count > 0 || cloudData.Notes.Count > 0)
                            {
                                foreach (var board in cloudData.Boards) state.Boards.Add(board);
                                foreach (var note in cloudData.Notes) state.Notes.Add(note);
                                state.NotifyChanged();
                            }
                        }
                        catch (System.Exception ex)
                        {
                            LogStartup($"[STARTUP] Cloud load failed: {ex.Message}");
                            startupVm.SetPhase(StartupPhase.OfflineReady, "Offline â€” using local data");
                            if (!ct.IsCancellationRequested)
                                await System.Threading.Tasks.Task.Delay(350, ct).ConfigureAwait(false);
                        }
                        if (ct.IsCancellationRequested) return false;
                        try { await syncService!.StartAsync(restoredUser.Id).ConfigureAwait(false); } catch (System.Exception ex) { LogStartup($"[STARTUP] Sync start failed: {ex.Message}"); }
                    }
                }

                // Transient offline for this session only — never persisted.
                // If the user picked Continue Offline, they stay offline until app close;
                // next launch re-shows the login. Persisted InitialSetupCompleted is now
                // only set on real login success; logout clears it.
                bool needAuth;
                {
                    bool loggedIn = restoredUser != null;
                    if (!loggedIn)
                        needAuth = true;
                    else
                        needAuth = false;
                    // Override: if the previous code had InitialSetupCompleted=false handling,
                    // we now just key off the session. This satisfies:
                    // - not logged in -> always show login
                    // - Continue Offline -> offline until close -> next launch shows login again
                    // - logged in -> skip auth
                    // - logged out -> show login again
                }
                LogStartup($"[STARTUP] needAuth(==no session)={needAuth} InitialSetupCompleted={state.Settings.InitialSetupCompleted} IsConfigured={config.IsConfigured}");

                if (needAuth)
                {
                    LogStartup("[STARTUP] Creating AuthenticationWindow");
                    var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
                    var authWebUrl = Environment.GetEnvironmentVariable("KANTAB_AUTH_WEB_URL")
                        ?? Environment.GetEnvironmentVariable("AUTH_WEB_URL")
                        ?? (config.IsConfigured ? null : null);

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        try
                        {
                            var svc = authService ?? new SupabaseAuthService(config);
                            bool isContinueOfflineFlow = false;
                            var authVm = new AuthenticationWindowViewModel(svc, () =>
                            {
                                // Real login: persist so next launch skips auth.
                                // Continue Offline: transient offline for this session only,
                                // do NOT persist InitialSetupCompleted so next launch shows login again.
                                if (isContinueOfflineFlow)
                                {
                                    LogStartup("[STARTUP] onSuccess(offline): transient offline, NOT persisting InitialSetupCompleted");
                                }
                                else
                                {
                                    LogStartup("[STARTUP] onSuccess(login): marking InitialSetupCompleted=true");
                                    state.Settings.InitialSetupCompleted = true;
                                    state.SaveNow();
                                }
                                tcs.TrySetResult(true);
                            }, canContinueOffline: true, authWebBaseUrl: authWebUrl);
                            authVm.ContinueOfflineRequested += (_, _) => isContinueOfflineFlow = true;
                            var authWindow = new AuthenticationWindow(authVm);
                            authVm.RequestClose += (_, _) => { LogStartup("[STARTUP] RequestClose -> Close()"); try { authWindow.Close(); } catch (System.Exception ex) { LogStartup($"[STARTUP] Close error: {ex}"); } };
                            authWindow.Closed += (_, _) => { LogStartup("[STARTUP] AuthenticationWindow.Closed"); if (!tcs.Task.IsCompleted) tcs.TrySetResult(false); };
                            authWindow.Opened += (_, _) => LogStartup("[STARTUP] AuthenticationWindow.Opened");
                            // MainWindow is already shown (desktop.MainWindow assigned before RunStartupAsync), so Show(mainWindow) is valid.
                            try { authWindow.Show(mainWindow); LogStartup("[STARTUP] AuthenticationWindow.Show(mainWindow) called"); }
                            catch (System.Exception ex) { LogStartup($"[STARTUP] Show(owner) failed: {ex}, trying Show()"); authWindow.Show(); LogStartup("[STARTUP] AuthenticationWindow.Show() called"); }
                        }
                        catch (System.Exception ex)
                        {
                            LogStartup($"[STARTUP ERROR] Creating auth window: {ex}");
                            tcs.TrySetResult(false);
                        }
                    });
                    LogStartup("[STARTUP] Awaiting auth window close...");
                    await tcs.Task.ConfigureAwait(false);
                    LogStartup($"[STARTUP] Auth window closed. Result={tcs.Task.Result}, InitialSetupCompleted={state.Settings.InitialSetupCompleted}");
                    if (ct.IsCancellationRequested) return false;
                    try
                    {
                        var u2 = await (authService ?? new SupabaseAuthService(config)).RestoreSessionAsync().ConfigureAwait(false);
                        if (u2 != null && syncService != null) await syncService.StartAsync(u2.Id).ConfigureAwait(false);
                    }
                    catch { }
                }

                var kanBanViewModel = new KanBanViewModel(state);
                var tasksViewModel = new TasksViewModel(state, kanBanViewModel);
                var notesViewModel = new NotesViewModel(state);

                if (config.IsConfigured && authService != null && cloudRepository != null && syncService != null)
                    mainVm = new MainWindowViewModel(state, kanBanViewModel, tasksViewModel, notesViewModel, authService, cloudRepository, syncService);
                else
                    mainVm = new MainWindowViewModel(state, kanBanViewModel, tasksViewModel, notesViewModel, null, null, null);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (ct.IsCancellationRequested) return;
                    mainWindow.DataContext = mainVm;
                    if (originalShellContent != null)
                        mainWindow.Content = originalShellContent;
                    else
                        mainWindow.Content = root;
                    root.Content = null;
                    mainWindow.Opened += (_, _) => { try { mainVm.SetFileDialogService(new AvaloniaFileDialogService(mainWindow)); } catch { } };
                    try { mainVm.SetFileDialogService(new AvaloniaFileDialogService(mainWindow)); } catch { }
                    LogStartup("[STARTUP] MainWindow shell shown. KanTab ready.");
                });

                var notifier = new WindowsNotificationService();
                scheduler = new NotificationScheduler(state, notifier);
                scheduler.Start();
                desktop.ShutdownRequested += (_, _) =>
                {
                    try { scheduler?.Dispose(); } catch { }
                    try { state?.FlushPendingSave(); } catch { }
                    try { syncService?.Dispose(); } catch { }
                };

                startupVm.SetPhase(StartupPhase.Ready);
                return true;
            }
            catch (System.Exception ex) when (!ct.IsCancellationRequested)
            {
                LogStartup($"[STARTUP ERROR] {ex}");
                startupVm.SetPhase(StartupPhase.Failed, ex.Message);
                startupVm.SetRetry(async token => await DoInit().ConfigureAwait(false),
                    async token =>
                    {
                        startupVm.SetPhase(StartupPhase.OfflineReady, "Offline â€” using local data");
                        await System.Threading.Tasks.Task.Delay(300, token).ConfigureAwait(false);
                    });
                return false;
            }
        }

        await DoInit().ConfigureAwait(false);
        LogStartup("DoInit returned.");
    }

    private static void LogStartup(string msg)
    {
        try
        {
            var p = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "KanTab", "startup.log");
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            File.AppendAllText(p, System.DateTime.Now.ToString("HH:mm:ss.fff") + " " + msg + System.Environment.NewLine);
            System.Diagnostics.Debug.WriteLine(msg);
        }
        catch { }
    }
}

