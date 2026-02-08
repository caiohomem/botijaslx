namespace Botijas.Domain.DomainEvents;

public record OrderCompleted(Guid OrderId, Guid CustomerId) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
