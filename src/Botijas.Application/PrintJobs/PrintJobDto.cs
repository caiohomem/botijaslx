namespace Botijas.Application.PrintJobs;

public class PrintJobDto
{
    public Guid PrintJobId { get; set; }
    public Guid StoreId { get; set; }
    public int Quantity { get; set; }
    public string? TemplateId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
