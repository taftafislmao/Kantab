using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KanTab.Storage.Supabase;

namespace KanTab.ViewModels;

public enum AuthMode { Welcome, Login, SignUp }

public partial class AuthenticationWindowViewModel : ViewModelBase
{
    private readonly IAuthService _authService;
    private readonly Action? _onSuccess;
    private readonly string? _authWebBaseUrl;

    [ObservableProperty] private AuthMode _currentMode = AuthMode.Welcome;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _canContinueOffline;

    public event EventHandler? RequestClose;
    public event EventHandler? ContinueOfflineRequested;

    public bool IsWelcome => CurrentMode == AuthMode.Welcome;
    public bool IsLogin => CurrentMode == AuthMode.Login;
    public bool IsSignUp => CurrentMode == AuthMode.SignUp;

    partial void OnCurrentModeChanged(AuthMode value)
    {
        OnPropertyChanged(nameof(IsWelcome));
        OnPropertyChanged(nameof(IsLogin));
        OnPropertyChanged(nameof(IsSignUp));
        ErrorMessage = null;
    }

    public AuthenticationWindowViewModel(IAuthService authService, Action? onSuccess = null, bool canContinueOffline = false, string? authWebBaseUrl = null)
    {
        _authService = authService;
        _onSuccess = onSuccess;
        _canContinueOffline = canContinueOffline;
        _authWebBaseUrl = string.IsNullOrWhiteSpace(authWebBaseUrl) ? null : authWebBaseUrl.TrimEnd('/');
    }

    public AuthenticationWindowViewModel() : this(new SupabaseAuthService(new SupabaseConfig())) { }

    private void OpenBrowser(string path)
    {
        if (string.IsNullOrWhiteSpace(_authWebBaseUrl)) return;
        var url = _authWebBaseUrl + path;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { ErrorMessage = $"Could not open browser. Please visit {url}"; }
    }

    [RelayCommand]
    private void ShowLogin()
    {
        // Keep auth in-window even when browser URL is configured — browser flow is for later phase.
        CurrentMode = AuthMode.Login;
    }

    [RelayCommand]
    private void ShowSignUp()
    {
        CurrentMode = AuthMode.SignUp;
    }
    [RelayCommand] private void BackToWelcome() => CurrentMode = AuthMode.Welcome;
    [RelayCommand] private void ContinueOffline()
    {
        ContinueOfflineRequested?.Invoke(this, EventArgs.Empty);
        _onSuccess?.Invoke();
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task LoginAsync(CancellationToken ct)
    {
        if (IsBusy) return;
        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(Email) || !Email.Contains('@'))
        { ErrorMessage = "Please enter a valid email address."; return; }
        if (string.IsNullOrWhiteSpace(Password))
        { ErrorMessage = "Please enter your password."; return; }
        IsBusy = true;
        try
        {
            var res = await _authService.SignInAsync(Email.Trim(), Password).ConfigureAwait(false);
            if (res.Success) { _onSuccess?.Invoke(); RequestClose?.Invoke(this, EventArgs.Empty); }
            else ErrorMessage = res.Error ?? "Login failed.";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SignUpAsync(CancellationToken ct)
    {
        if (IsBusy) return;
        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(Email) || !Email.Contains('@'))
        { ErrorMessage = "Please enter a valid email address."; return; }
        if (string.IsNullOrWhiteSpace(Password) || Password.Length < 6)
        { ErrorMessage = "Password must be at least 6 characters."; return; }
        if (Password != ConfirmPassword)
        { ErrorMessage = "Passwords do not match."; return; }
        IsBusy = true;
        try
        {
            var res = await _authService.SignUpAsync(Email.Trim(), Password).ConfigureAwait(false);
            if (res.Success)
            {
                if (_authService.CurrentUser == null)
                {
                    ErrorMessage = "Check your email to confirm your account, then log in.";
                    CurrentMode = AuthMode.Login;
                    return;
                }
                _onSuccess?.Invoke();
                RequestClose?.Invoke(this, EventArgs.Empty);
            }
            else ErrorMessage = res.Error ?? "Sign up failed.";
        }
        finally { IsBusy = false; }
    }
}
