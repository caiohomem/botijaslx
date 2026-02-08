namespace Botijas.Domain.DomainEvents;

public record PrintJobPrinted(Guid PrintJobId) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
