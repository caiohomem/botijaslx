namespace Botijas.Domain.DomainEvents;

public record PrintJobFailed(Guid PrintJobId, string Error) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
