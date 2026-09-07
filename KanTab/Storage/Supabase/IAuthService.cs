using System.Threading.Tasks;

namespace KanTab.Storage.Supabase;

/// <summary>
/// Authentication boundary for the desktop app. Implementations hide all
/// Supabase SDK/HTTP details from the ViewModels.
/// </summary>
public interface IAuthService
{
    /// <summary>Restores a previously stored session (or refreshes it) on app start.</summary>
    Task<AuthUser?> RestoreSessionAsync();

    Task<AuthResult> SignInAsync(string email, string password);
    Task<AuthResult> SignUpAsync(string email, string password);
    Task SignOutAsync();

    AuthUser? CurrentUser { get; }

    /// <summary>Returns the current access token, or null if not signed in.</summary>
    string? GetCurrentAccessToken();
}
