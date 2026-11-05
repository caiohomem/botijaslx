using Botijas.Domain.Entities;

namespace Botijas.Domain.Repositories;

public interface IRefillOrderRepository
{
    Task<RefillOrder?> FindByIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<RefillOrder?> FindByCylinderIdAsync(Guid cylinderId, CancellationToken cancellationToken = default);
    Task<List<CurrentCylinderOrderInfo>> FindCurrentCylinderOrdersByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<List<RefillOrder>> FindOpenOrdersByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<List<RefillOrder>> FindAllByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<List<RefillOrder>> FindReadyForPickupAsync(Guid? customerId = null, CancellationToken cancellationToken = default);
    Task<Dictionary<Guid, DateTime>> GetReadyAtByOrderAsync(CancellationToken cancellationToken = default);
    Task AddAsync(RefillOrder order, CancellationToken cancellationToken = default);
    Task DeleteAsync(RefillOrder order, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializa alterações ao mesmo pedido. Sem este lock, marcações de "cheia"
    /// em paralelo gravam todas as botijas como Ready e a última escrita deixa o
    /// pedido Open — as botijas desaparecem da fila e não entram na Out.
    /// </summary>
    Task ExecuteLockedAsync(Guid orderId, Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
}

public class CurrentCylinderOrderInfo
{
    public Guid CylinderId { get; set; }
    public Guid OrderId { get; set; }
    public RefillOrderStatus OrderStatus { get; set; }
    public DateTime OrderCreatedAt { get; set; }
    public FulfillmentMethod FulfillmentMethod { get; set; }
    public bool RefillPaid { get; set; }
    public bool ShippingPaid { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationNotes { get; set; }
}
