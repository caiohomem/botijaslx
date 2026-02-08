using Botijas.Domain.Entities;
using Botijas.Domain.Repositories;
using Botijas.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Botijas.Infrastructure.Repositories;

public class PrintJobRepository : IPrintJobRepository
{
    private readonly BotijasDbContext _context;

    public PrintJobRepository(BotijasDbContext context)
    {
        _context = context;
    }

    public async Task<PrintJob?> FindByIdAsync(Guid printJobId, CancellationToken cancellationToken = default)
    {
        return await _context.PrintJobs
            .FirstOrDefaultAsync(pj => pj.PrintJobId == printJobId, cancellationToken);
    }

    public async Task<List<PrintJob>> FindPendingAsync(CancellationToken cancellationToken = default)
    {
        return await _context.PrintJobs
            .Where(pj => pj.Status == PrintJobStatus.Pending)
            .OrderBy(pj => pj.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(PrintJob printJob, CancellationToken cancellationToken = default)
    {
        await _context.PrintJobs.AddAsync(printJob, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
