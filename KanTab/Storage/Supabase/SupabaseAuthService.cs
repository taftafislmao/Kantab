using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace KanTab.Storage.Supabase;

/// <summary>
/// Supabase Auth (GoTrue) implementation over the REST API. Uses only the
/// project URL and public anonymous key; the service-role key is never used.
/// </summary>
public class SupabaseAuthService : IAuthService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly SupabaseConfig _config;
    private readonly string _sessionFilePath;
    private readonly object _gate = new();
    private string? _accessToken;

    public AuthUser? CurrentUser { get; private set; }

    public string? GetCurrentAccessToken()
    {
        lock (_gate)
        {
            return _accessToken;
        }
    }

    public SupabaseAuthService(SupabaseConfig config, string? sessionFilePath = null, HttpClient? httpClient = null)
    {
        _config = config;
        _sessionFilePath = sessionFilePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KanTab", "session.json");
        _http = httpClient ?? new HttpClient();
        _http.DefaultRequestHeaders.Add("apikey", _config.AnonKey);
    }

    // ---------- session persistence ----------

    public SupabaseSession? LoadStoredSession()
    {
        try
        {
            if (!File.Exists(_sessionFilePath))
                return null;
            var json = File.ReadAllText(_sessionFilePath);
            if (string.IsNullOrWhiteSpace(json))
                return null;
            return JsonSerializer.Deserialize<SupabaseSession>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private void StoreSession(SupabaseSession? session)
    {
        try
        {
            var directory = Path.GetDirectoryName(_sessionFilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            if (session == null)
            {
                if (File.Exists(_sessionFilePath))
                    File.Delete(_sessionFilePath);
                return;
            }

            var temporaryPath = _sessionFilePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(session, JsonOptions));
            File.Move(temporaryPath, _sessionFilePath, true);
        }
        catch
        {
            // Session persistence must never break the app.
        }
    }

    // ---------- IAuthService ----------

    public async Task<AuthUser?> RestoreSessionAsync()
    {
        if (!_config.IsConfigured)
            return null;

        var session = LoadStoredSession();
        if (session == null || string.IsNullOrEmpty(session.RefreshToken))
            return null;

        try
        {
            if (session.ExpiresAt == default || session.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            {
                var user = await FetchUserAsync(session.AccessToken).ConfigureAwait(false);
                if (user != null)
                {
                    if (session.ExpiresAt == default)
                        session.ExpiresAt = DateTimeOffset.UtcNow.AddHours(1);
                    ApplySession(session);
                    return user;
                }
            }

            return await RefreshAsync(session.RefreshToken).ConfigureAwait(false);
        }
        catch
        {
            // Offline or server error: stay signed in locally but do NOT set _accessToken,
            // so authenticated requests won't use a stale token.
            CurrentUser = new AuthUser { Id = session.UserId, Email = session.Email };
            lock (_gate) { _accessToken = null; }
            return CurrentUser;
        }
    }

    public Task<AuthResult> SignInAsync(string email, string password)
    {
        if (!_config.IsConfigured)
            return Task.FromResult(AuthResult.Fail("Supabase is not configured. KanTab is running in local-only mode."));
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return Task.FromResult(AuthResult.Fail("Please enter a valid email address."));
        if (string.IsNullOrWhiteSpace(password))
            return Task.FromResult(AuthResult.Fail("Please enter your password."));

        var body = JsonSerializer.Serialize(new { email, password }, JsonOptions);
        return SendAuthAsync("/auth/v1/token?grant_type=password", body);
    }

    public Task<AuthResult> SignUpAsync(string email, string password)
    {
        if (!_config.IsConfigured)
            return Task.FromResult(AuthResult.Fail("Supabase is not configured. KanTab is running in local-only mode."));
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return Task.FromResult(AuthResult.Fail("Please enter a valid email address."));
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            return Task.FromResult(AuthResult.Fail("Password must be at least 6 characters."));

        var body = JsonSerializer.Serialize(new { email, password }, JsonOptions);
        return SendAuthAsync("/auth/v1/signup", body);
    }

    public async Task SignOutAsync()
    {
        var session = LoadStoredSession();
        if (_config.IsConfigured && !string.IsNullOrEmpty(session?.AccessToken))
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, _config.Url + "/auth/v1/logout");
                AddAuthHeaders(request, session!.AccessToken);
                await _http.SendAsync(request).ConfigureAwait(false);
            }
            catch
            {
                // Sign out locally regardless of the server response.
            }
        }

        lock (_gate)
        {
            CurrentUser = null;
            _accessToken = null;
        }
        StoreSession(null);
        try { ClearOutboxFile(); } catch { }
    }

    private void ClearOutboxFile()
    {
        var outboxPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KanTab", "outbox.json");
        if (File.Exists(outboxPath)) File.Delete(outboxPath);
    }

    // ----- Private helpers -----

    public void Dispose()
    {
        // HttpClient disposal is handled by the caller if provided.
    }

    private async Task<AuthUser?> FetchUserAsync(string accessToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _config.Url + "/auth/v1/user");
            AddAuthHeaders(request, accessToken);
            var response = await _http.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var userId = root.TryGetProperty("id", out var id) ? id.GetString() : null;
            var email = root.TryGetProperty("email", out var e) ? e.GetString() : null;

            if (string.IsNullOrEmpty(userId))
                return null;

            return new AuthUser { Id = userId, Email = email ?? string.Empty };
        }
        catch
        {
            return null;
        }
    }

    private void ApplySession(SupabaseSession session)
    {
        lock (_gate)
        {
            CurrentUser = new AuthUser { Id = session.UserId, Email = session.Email };
            _accessToken = session.AccessToken;
        }
    }

    private async Task<AuthUser?> RefreshAsync(string refreshToken)
    {
        try
        {
            var body = JsonSerializer.Serialize(new { refresh_token = refreshToken }, JsonOptions);
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync(_config.Url + "/auth/v1/token?grant_type=refresh_token", content).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                // Permanent failure: clear stale session so we don't keep retrying bad tokens.
                StoreSession(null);
                lock (_gate) { CurrentUser = null; _accessToken = null; }
                return null;
            }

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var accessToken = root.TryGetProperty("access_token", out var t) ? t.GetString() : null;
            var newRefreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
            var userId = root.TryGetProperty("user", out var u) && u.TryGetProperty("id", out var id) ? id.GetString() : null;
            var email = root.TryGetProperty("user", out var e) ? e.GetString() : null;

            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(userId))
                return null;

            var session = new SupabaseSession
            {
                AccessToken = accessToken,
                RefreshToken = !string.IsNullOrEmpty(newRefreshToken) ? newRefreshToken : refreshToken,
                UserId = userId,
                Email = email ?? string.Empty,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
            };
            StoreSession(session);
            ApplySession(session);
            return new AuthUser { Id = userId, Email = email ?? string.Empty };
        }
        catch
        {
            return null;
        }
    }

    private void AddAuthHeaders(HttpRequestMessage request, string accessToken)
    {
        request.Headers.Add("apikey", _config.AnonKey);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
    }

    private async Task<AuthResult> SendAuthAsync(string endpoint, string body)
    {
        try
        {
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync(_config.Url + endpoint, content).ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(json);
                var error = doc.RootElement.TryGetProperty("error_description", out var desc)
                    ? desc.GetString()
                    : doc.RootElement.TryGetProperty("message", out var msg)
                        ? msg.GetString()
                        : "Authentication failed.";
                return AuthResult.Fail(error ?? "Authentication failed.");
            }

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var accessToken = root.TryGetProperty("access_token", out var t) ? t.GetString() : null;
            var refreshToken = root.TryGetProperty("refresh_token", out var r) ? r.GetString() : null;
            var userId = root.TryGetProperty("user", out var u) && u.TryGetProperty("id", out var uid)
                ? uid.GetString() : null;
            var email = root.TryGetProperty("user", out var u2) && u2.TryGetProperty("email", out var e)
                ? e.GetString() : null;

            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(userId))
                return AuthResult.Fail("Invalid response from authentication server.");

            var session = new SupabaseSession
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken ?? string.Empty,
                UserId = userId,
                Email = email ?? string.Empty,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
            };
            StoreSession(session);
            ApplySession(session);
            return AuthResult.Ok(CurrentUser!);
        }
        catch (Exception ex)
        {
            return AuthResult.Fail($"Network error: {ex.Message}");
        }
    }
}
