using Botijas.Domain.Entities;

namespace Botijas.Domain.Repositories;

public interface IPrintJobRepository
{
    Task<PrintJob?> FindByIdAsync(Guid printJobId, CancellationToken cancellationToken = default);
    Task<List<PrintJob>> FindPendingAsync(CancellationToken cancellationToken = default);
    Task AddAsync(PrintJob printJob, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
