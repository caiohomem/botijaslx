using Botijas.Domain.DomainEvents;

namespace Botijas.Domain.Entities;

public enum RefillOrderStatus
{
    Open,
    ReadyForPickup,
    Completed
}

public class RefillOrder
{
    public Guid OrderId { get; private set; }
    public Guid CustomerId { get; private set; }
    public RefillOrderStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? NotifiedAt { get; private set; }

    private readonly List<CylinderRef> _cylinders = new();
    public IReadOnlyCollection<CylinderRef> Cylinders => _cylinders.AsReadOnly();

    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private RefillOrder() // EF Core
    {
        Status = RefillOrderStatus.Open;
        CreatedAt = DateTime.UtcNow;
    }

    private RefillOrder(Guid orderId, Guid customerId)
    {
        OrderId = orderId;
        CustomerId = customerId;
        Status = RefillOrderStatus.Open;
        CreatedAt = DateTime.UtcNow;
    }

    public static RefillOrder Create(Guid customerId)
    {
        var order = new RefillOrder(Guid.NewGuid(), customerId);
        order._domainEvents.Add(new OrderCreated(order.OrderId, order.CustomerId));
        return order;
    }

    public void AddCylinder(Cylinder cylinder)
    {
        if (Status != RefillOrderStatus.Open)
        {
            throw new InvalidOperationException($"Cannot add cylinder to order. Current status: {Status}");
        }

        if (_cylinders.Any(c => c.CylinderId == cylinder.CylinderId))
        {
            throw new InvalidOperationException("Cylinder already added to this order");
        }

        _cylinders.Add(new CylinderRef(OrderId, cylinder.CylinderId));
    }

    public void CheckAndUpdateStatus(IEnumerable<Cylinder> cylinders)
    {
        if (Status != RefillOrderStatus.Open)
        {
            return;
        }

        // Atualizar estados dos CylinderRefs baseado nos Cylinders reais
        var cylinderDict = cylinders.ToDictionary(c => c.CylinderId);
        foreach (var cylinderRef in _cylinders)
        {
            if (cylinderDict.TryGetValue(cylinderRef.CylinderId, out var cylinder))
            {
                cylinderRef.State = cylinder.State;
            }
        }

        var allReady = _cylinders.Count > 0 && 
                      _cylinders.All(c => c.State == CylinderState.Ready);

        if (allReady && Status == RefillOrderStatus.Open)
        {
            Status = RefillOrderStatus.ReadyForPickup;
            _domainEvents.Add(new OrderBecameReadyForPickup(OrderId, CustomerId, _cylinders.Count));
        }
    }

    public void Complete()
    {
        if (Status != RefillOrderStatus.ReadyForPickup)
        {
            throw new InvalidOperationException($"Cannot complete order. Current status: {Status}");
        }

        var allDelivered = _cylinders.All(c => c.State == CylinderState.Delivered);
        if (!allDelivered)
        {
            throw new InvalidOperationException("Cannot complete order. Not all cylinders are delivered");
        }

        Status = RefillOrderStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        _domainEvents.Add(new OrderCompleted(OrderId, CustomerId));
    }

    public void MarkAsNotified()
    {
        if (Status != RefillOrderStatus.ReadyForPickup)
        {
            throw new InvalidOperationException($"Cannot mark order as notified. Current status: {Status}");
        }

        NotifiedAt = DateTime.UtcNow;
    }

    public bool NeedsNotification => Status == RefillOrderStatus.ReadyForPickup && NotifiedAt == null;

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}

// Entidade de relacionamento para EF Core
public class CylinderRef
{
    public Guid OrderId { get; set; }
    public Guid CylinderId { get; set; }
    public CylinderState State { get; set; } // Mantido sincronizado com Cylinder

    private CylinderRef() { } // EF Core

    public CylinderRef(Guid orderId, Guid cylinderId)
    {
        OrderId = orderId;
        CylinderId = cylinderId;
        State = CylinderState.Received;
    }
}
