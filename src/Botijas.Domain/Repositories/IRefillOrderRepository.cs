using Botijas.Domain.Entities;

namespace Botijas.Domain.Repositories;

public interface IRefillOrderRepository
{
    Task<RefillOrder?> FindByIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<RefillOrder?> FindByCylinderIdAsync(Guid cylinderId, CancellationToken cancellationToken = default);
    Task<List<RefillOrder>> FindOpenOrdersByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<List<RefillOrder>> FindAllByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<List<RefillOrder>> FindReadyForPickupAsync(Guid? customerId = null, CancellationToken cancellationToken = default);
    Task AddAsync(RefillOrder order, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
