namespace Botijas.Application.Orders;

public class OrderDto
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string FulfillmentMethod { get; set; } = string.Empty;
    public bool RefillPaid { get; set; }
    public bool ShippingPaid { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationNotes { get; set; }
    public int CylinderCount { get; set; }
}
