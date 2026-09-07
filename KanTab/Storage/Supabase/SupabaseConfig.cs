using System;
using System.IO;
using System.Text.Json;

namespace KanTab.Storage.Supabase;

/// <summary>
/// Supabase project configuration for the desktop client.
///
/// Only the project URL and the public anonymous key may be stored here.
/// The service-role key must NEVER be placed in the desktop client — it
/// bypasses Row Level Security and would expose every user's data.
///
/// Configuration is read from (first match wins):
///   1. Environment variables SUPABASE_URL / SUPABASE_ANON_KEY
///   2. %LOCALAPPDATA%\KanTab\supabase.json  ({"url": "...", "anonKey": "..."})
///   3. supabase.local.json next to the executable (git-ignored dev file)
/// </summary>
public class SupabaseConfig
{
    public string Url { get; init; } = string.Empty;
    public string AnonKey { get; init; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Url) && !string.IsNullOrWhiteSpace(AnonKey);

    public static SupabaseConfig Load()
    {
        var url = Environment.GetEnvironmentVariable("KANTAB_SUPABASE_URL")
            ?? Environment.GetEnvironmentVariable("SUPABASE_URL");
        var anonKey = Environment.GetEnvironmentVariable("KANTAB_SUPABASE_ANON_KEY")
            ?? Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY");

        if (!string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(anonKey))
            return new SupabaseConfig { Url = url.TrimEnd('/'), AnonKey = anonKey };

        foreach (var path in new[]
                 {
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KanTab", "supabase.json"),
                     Path.Combine(AppContext.BaseDirectory, "supabase.local.json"),
                     Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "supabase.local.json"),
                     Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "supabase.local.json")
                 })
        {
            try
            {
                if (!File.Exists(path))
                    continue;

                using var document = JsonDocument.Parse(File.ReadAllText(path));
                var root = document.RootElement;
                url = root.TryGetProperty("url", out var u) ? u.GetString() : null;
                anonKey = root.TryGetProperty("anonKey", out var k) ? k.GetString() : null;
                if (!string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(anonKey))
                    return new SupabaseConfig { Url = url.TrimEnd('/'), AnonKey = anonKey };
            }
            catch
            {
                // A malformed local config file must not prevent local-only mode.
            }
        }

        return new SupabaseConfig();
    }
}
