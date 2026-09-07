using System.Collections.Generic;
using System.Threading.Tasks;

namespace KanTab.Storage.Supabase;

public interface IOutboxRepository
{
    Task<List<OutboxEntry>> GetAllPendingAsync();
    Task AddAsync(OutboxEntry entry);
    Task UpdateAsync(OutboxEntry entry);
    Task RemoveAsync(string operationId);
    Task<bool> HasPendingAsync();
}
