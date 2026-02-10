using Botijas.Domain.Entities;
using Botijas.Domain.ValueObjects;

namespace Botijas.Domain.Repositories;

public interface ICylinderRepository
{
    Task<Cylinder?> FindByIdAsync(Guid cylinderId, CancellationToken cancellationToken = default);
    Task<Cylinder?> FindByLabelTokenAsync(LabelToken labelToken, CancellationToken cancellationToken = default);
    Task<Cylinder?> FindInOpenOrderAsync(Guid cylinderId, CancellationToken cancellationToken = default);
    Task<List<Cylinder>> FindByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<List<FillingQueueItem>> GetFillingQueueAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Cylinder cylinder, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

// DTO para fila de enchimento (usado pelo repositório)
public class FillingQueueItem
{
    public Guid CylinderId { get; set; }
    public long SequentialNumber { get; set; }
    public string? LabelToken { get; set; }
    public string State { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public Guid OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public int TotalCylindersInOrder { get; set; }
    public int ReadyCylindersInOrder { get; set; }
}
