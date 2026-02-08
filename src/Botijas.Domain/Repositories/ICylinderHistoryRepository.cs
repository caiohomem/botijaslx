using Botijas.Domain.Entities;

namespace Botijas.Domain.Repositories;

public interface ICylinderHistoryRepository
{
    Task<List<CylinderHistoryEntry>> GetByCylinderIdAsync(Guid cylinderId, CancellationToken cancellationToken = default);
    Task AddAsync(CylinderHistoryEntry entry, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
