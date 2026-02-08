namespace Botijas.Application.PrintJobs.Commands;

public record CreatePrintJobCommand(
    Guid StoreId, 
    int Quantity, 
    string? TemplateId,
    string? CustomerName = null,
    string? CustomerPhone = null);
