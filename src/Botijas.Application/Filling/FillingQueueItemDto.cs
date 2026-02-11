namespace Botijas.Application.Filling;

public class FillingQueueItemDto
{
    public Guid CylinderId { get; set; }
    public long SequentialNumber { get; set; }
    public string? LabelToken { get; set; }
    public string State { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    
    // Contexto do pedido
    public Guid OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public int TotalCylindersInOrder { get; set; }
    public int ReadyCylindersInOrder { get; set; }
}
