namespace Botijas.Application.PrintJobs;

public interface IPrintJobDispatcher
{
    Task DispatchAsync(
        Guid printJobId, 
        int quantity, 
        string? templateId,
        string? customerName,
        string? customerPhone,
        CancellationToken cancellationToken);
}
