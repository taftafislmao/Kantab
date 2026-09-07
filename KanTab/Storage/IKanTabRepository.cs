using System.Threading.Tasks;

namespace KanTab.Storage;

/// <summary>
/// Persistence boundary for all KanTab data. The desktop app uses a local JSON
/// implementation; a future Supabase-backed repository can implement the same
/// interface without changes to the ViewModels.
/// </summary>
public interface IKanTabRepository
{
    /// <summary>Loads the persisted data. Never throws for recoverable problems.</summary>
    Task<KanTabData> LoadAsync();

    /// <summary>Persists the data. Throws only when saving is impossible.</summary>
    Task SaveAsync(KanTabData data);
}

/// <summary>
/// Cloud persistence boundary for all KanTab data. Mirrors IKanTabRepository but
/// with user-specific operations for Supabase cloud storage.
/// </summary>
public interface ICloudKanTabRepository
{
    /// <summary>Loads the user's data from Supabase. Never throws for recoverable problems.</summary>
    Task<KanTabData> LoadAsync(string userId);

    /// <summary>Persists the user's data to Supabase. Throws only when saving is impossible.</summary>
    Task SaveAsync(KanTabData data, string userId);
}
