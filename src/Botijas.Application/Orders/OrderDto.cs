namespace Botijas.Application.Orders;

public class OrderDto
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int CylinderCount { get; set; }
}
