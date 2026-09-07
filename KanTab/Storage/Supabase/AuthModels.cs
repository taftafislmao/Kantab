using System;

namespace KanTab.Storage.Supabase;

/// <summary>The signed-in desktop user, decoupled from any SDK types.</summary>
public class AuthUser
{
    public string Id { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}

/// <summary>Outcome of a sign-in / sign-up attempt.</summary>
public class AuthResult
{
    public bool Success { get; init; }
    public AuthUser? User { get; init; }
    public string? Error { get; init; }

    public static AuthResult Ok(AuthUser user) => new() { Success = true, User = user };
    public static AuthResult Fail(string error) => new() { Success = false, Error = error };
}

/// <summary>Persisted Supabase session (stored only on this machine).</summary>
public class SupabaseSession
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
}
