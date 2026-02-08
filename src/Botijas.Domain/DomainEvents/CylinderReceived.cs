namespace Botijas.Domain.DomainEvents;

public record CylinderReceived(Guid CylinderId, string? LabelToken) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
