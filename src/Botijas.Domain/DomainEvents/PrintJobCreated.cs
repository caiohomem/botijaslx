namespace Botijas.Domain.DomainEvents;

public record PrintJobCreated(Guid PrintJobId, Guid StoreId, int Quantity, string? TemplateId) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
