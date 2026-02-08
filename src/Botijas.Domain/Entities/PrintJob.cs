using Botijas.Domain.DomainEvents;

namespace Botijas.Domain.Entities;

public enum PrintJobStatus
{
    Pending,
    Dispatched,
    Printed,
    Failed
}

public class PrintJob
{
    public Guid PrintJobId { get; private set; }
    public Guid StoreId { get; private set; }
    public int Quantity { get; private set; }
    public string? TemplateId { get; private set; }
    public PrintJobStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private PrintJob() // EF Core
    {
        Status = PrintJobStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    private PrintJob(Guid printJobId, Guid storeId, int quantity, string? templateId)
    {
        PrintJobId = printJobId;
        StoreId = storeId;
        Quantity = quantity;
        TemplateId = templateId;
        Status = PrintJobStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public static PrintJob Create(Guid storeId, int quantity, string? templateId = null)
    {
        if (quantity <= 0)
        {
            throw new ArgumentException("Quantity must be greater than zero", nameof(quantity));
        }

        var job = new PrintJob(Guid.NewGuid(), storeId, quantity, templateId);
        job._domainEvents.Add(new PrintJobCreated(job.PrintJobId, job.StoreId, job.Quantity, job.TemplateId));
        return job;
    }

    public void MarkAsDispatched()
    {
        if (Status != PrintJobStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot mark as Dispatched. Current status: {Status}");
        }

        Status = PrintJobStatus.Dispatched;
    }

    public void MarkAsPrinted()
    {
        if (Status != PrintJobStatus.Dispatched)
        {
            throw new InvalidOperationException($"Cannot mark as Printed. Current status: {Status}");
        }

        Status = PrintJobStatus.Printed;
        CompletedAt = DateTime.UtcNow;
        _domainEvents.Add(new PrintJobPrinted(PrintJobId));
    }

    public void MarkAsFailed(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            throw new ArgumentException("Error message cannot be empty", nameof(error));
        }

        Status = PrintJobStatus.Failed;
        ErrorMessage = error;
        CompletedAt = DateTime.UtcNow;
        _domainEvents.Add(new PrintJobFailed(PrintJobId, error));
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
