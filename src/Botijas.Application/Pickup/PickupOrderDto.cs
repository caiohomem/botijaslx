namespace Botijas.Application.Pickup;

public class PickupOrderDto
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? NotifiedAt { get; set; }
    public bool NeedsNotification { get; set; }
    public int TotalCylinders { get; set; }
    public int DeliveredCylinders { get; set; }
    public List<PickupCylinderDto> Cylinders { get; set; } = new();
}

public class PickupCylinderDto
{
    public Guid CylinderId { get; set; }
    public long SequentialNumber { get; set; }
    public string? LabelToken { get; set; }
    public string State { get; set; } = string.Empty;
    public bool IsDelivered { get; set; }
}
