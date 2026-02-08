namespace Botijas.Domain.Entities;

public enum CylinderEventType
{
    Received,
    LabelAssigned,
    MarkedReady,
    Delivered,
    ProblemReported
}

public class CylinderHistoryEntry
{
    public Guid Id { get; private set; }
    public Guid CylinderId { get; private set; }
    public CylinderEventType EventType { get; private set; }
    public string? Details { get; private set; }
    public Guid? OrderId { get; private set; }
    public DateTime Timestamp { get; private set; }

    private CylinderHistoryEntry() { } // EF Core

    public static CylinderHistoryEntry Create(
        Guid cylinderId,
        CylinderEventType eventType,
        string? details = null,
        Guid? orderId = null)
    {
        return new CylinderHistoryEntry
        {
            Id = Guid.NewGuid(),
            CylinderId = cylinderId,
            EventType = eventType,
            Details = details,
            OrderId = orderId,
            Timestamp = DateTime.UtcNow
        };
    }
}
