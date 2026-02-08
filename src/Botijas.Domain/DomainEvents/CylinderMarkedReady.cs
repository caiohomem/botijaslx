namespace Botijas.Domain.DomainEvents;

public record CylinderMarkedReady(Guid CylinderId) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
