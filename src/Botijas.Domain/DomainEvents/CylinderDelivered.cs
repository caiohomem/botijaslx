namespace Botijas.Domain.DomainEvents;

public record CylinderDelivered(Guid CylinderId) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
