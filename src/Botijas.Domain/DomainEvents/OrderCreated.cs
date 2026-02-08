namespace Botijas.Domain.DomainEvents;

public record OrderCreated(Guid OrderId, Guid CustomerId) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
