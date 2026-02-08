namespace Botijas.Domain.DomainEvents;

public record CustomerCreated(Guid CustomerId, string Name, string Phone) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
