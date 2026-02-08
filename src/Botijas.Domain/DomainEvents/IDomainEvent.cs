namespace Botijas.Domain.DomainEvents;

public interface IDomainEvent
{
    DateTime OccurredAt { get; }
}
