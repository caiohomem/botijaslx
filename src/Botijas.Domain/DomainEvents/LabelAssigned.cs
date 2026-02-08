namespace Botijas.Domain.DomainEvents;

public record LabelAssigned(Guid CylinderId, string LabelToken) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
