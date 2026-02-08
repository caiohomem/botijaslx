using Botijas.Domain.Entities;
using Botijas.Domain.Repositories;
using Botijas.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Botijas.Infrastructure.Repositories;

public class CylinderHistoryRepository : ICylinderHistoryRepository
{
    private readonly BotijasDbContext _context;

    public CylinderHistoryRepository(BotijasDbContext context)
    {
        _context = context;
    }

    public async Task<List<CylinderHistoryEntry>> GetByCylinderIdAsync(
        Guid cylinderId,
        CancellationToken cancellationToken = default)
    {
        return await _context.CylinderHistory
            .Where(h => h.CylinderId == cylinderId)
            .OrderByDescending(h => h.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(CylinderHistoryEntry entry, CancellationToken cancellationToken = default)
    {
        await _context.CylinderHistory.AddAsync(entry, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
