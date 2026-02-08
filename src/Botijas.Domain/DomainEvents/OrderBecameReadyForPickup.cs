namespace Botijas.Domain.DomainEvents;

public record OrderBecameReadyForPickup(Guid OrderId, Guid CustomerId, int CylinderCount) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
