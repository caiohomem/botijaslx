using Botijas.Domain.DomainEvents;

namespace Botijas.Domain.Entities;

public enum RefillOrderStatus
{
    Open,
    ReadyForPickup,
    Completed,
    Cancelled
}

public enum FulfillmentMethod
{
    Pickup,
    Shipping
}

public class RefillOrder
{
    public Guid OrderId { get; private set; }
    public Guid CustomerId { get; private set; }
    public RefillOrderStatus Status { get; private set; }
    public FulfillmentMethod FulfillmentMethod { get; private set; }
    public bool RefillPaid { get; private set; }
    public bool ShippingPaid { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? NotifiedAt { get; private set; }
    public DateTime? ShippedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationNotes { get; private set; }

    private readonly List<CylinderRef> _cylinders = new();
    public IReadOnlyCollection<CylinderRef> Cylinders => _cylinders.AsReadOnly();

    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private RefillOrder() // EF Core
    {
        Status = RefillOrderStatus.Open;
        FulfillmentMethod = FulfillmentMethod.Pickup;
        CreatedAt = DateTime.UtcNow;
    }

    private RefillOrder(
        Guid orderId,
        Guid customerId,
        FulfillmentMethod fulfillmentMethod,
        bool refillPaid,
        bool shippingPaid)
    {
        OrderId = orderId;
        CustomerId = customerId;
        Status = RefillOrderStatus.Open;
        FulfillmentMethod = fulfillmentMethod;
        RefillPaid = refillPaid;
        ShippingPaid = shippingPaid;
        CreatedAt = DateTime.UtcNow;
    }

    public static RefillOrder Create(
        Guid customerId,
        FulfillmentMethod fulfillmentMethod = FulfillmentMethod.Pickup,
        bool refillPaid = false,
        bool shippingPaid = false)
    {
        var order = new RefillOrder(Guid.NewGuid(), customerId, fulfillmentMethod, refillPaid, shippingPaid);
        order._domainEvents.Add(new OrderCreated(order.OrderId, order.CustomerId));
        return order;
    }

    public void UpdateFulfillmentDetails(
        FulfillmentMethod fulfillmentMethod,
        bool refillPaid,
        bool shippingPaid)
    {
        if (Status != RefillOrderStatus.Open)
        {
            throw new InvalidOperationException($"Cannot update fulfillment details. Current status: {Status}");
        }

        FulfillmentMethod = fulfillmentMethod;
        RefillPaid = refillPaid;
        ShippingPaid = shippingPaid;
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

    public void Cancel(string notes)
    {
        if (Status == RefillOrderStatus.Completed)
        {
            throw new InvalidOperationException("Cannot cancel a completed order");
        }

        if (Status == RefillOrderStatus.Cancelled)
        {
            throw new InvalidOperationException("Order is already cancelled");
        }

        if (string.IsNullOrWhiteSpace(notes))
        {
            throw new ArgumentException("Cancellation notes cannot be empty", nameof(notes));
        }

        Status = RefillOrderStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
        CancellationNotes = notes.Trim();
        CompletedAt = null;
        NotifiedAt = null;
        ShippedAt = null;
    }

    /// <summary>
    /// Recalcula o status do pedido a partir do estado atual das botijas (Cylinder.State).
    /// Pedidos terminal (Completed/Cancelled) não são alterados — a botija pode já estar noutro ciclo.
    /// CylinderRef.State deixa de ser fonte de verdade (espelho legado, a remover).
    /// </summary>
    public void RecalculateStatus(IEnumerable<Cylinder> cylinders)
    {
        if (Status is RefillOrderStatus.Cancelled or RefillOrderStatus.Completed)
        {
            return;
        }

        var cylinderDict = cylinders.ToDictionary(c => c.CylinderId);
        var orderCylinders = _cylinders
            .Select(cylinderRef => cylinderDict.TryGetValue(cylinderRef.CylinderId, out var cylinder) ? cylinder : null)
            .Where(c => c != null)
            .Select(c => c!)
            .ToList();

        if (orderCylinders.Count == 0 || orderCylinders.Count != _cylinders.Count)
        {
            return;
        }

        // Espelho legado: mantido só até a coluna CylinderRef.State ser removida.
        foreach (var cylinderRef in _cylinders)
        {
            if (cylinderDict.TryGetValue(cylinderRef.CylinderId, out var cylinder))
            {
                cylinderRef.State = cylinder.State;
            }
        }

        var allDelivered = orderCylinders.All(c => c.State == CylinderState.Delivered);
        if (allDelivered)
        {
            Status = RefillOrderStatus.Completed;
            CompletedAt ??= DateTime.UtcNow;
            return;
        }

        var readyForPickup = orderCylinders.All(c => IsCylinderReadyForPickup(c.State));
        if (readyForPickup)
        {
            if (Status != RefillOrderStatus.ReadyForPickup)
            {
                Status = RefillOrderStatus.ReadyForPickup;
                _domainEvents.Add(new OrderBecameReadyForPickup(OrderId, CustomerId, _cylinders.Count));
            }

            CompletedAt = null;
            return;
        }

        Status = RefillOrderStatus.Open;
        CompletedAt = null;
        NotifiedAt = null;
        ShippedAt = null;
        CancelledAt = null;
        CancellationNotes = null;
    }

    public void Complete(IEnumerable<Cylinder> cylinders)
    {
        if (Status != RefillOrderStatus.ReadyForPickup)
        {
            throw new InvalidOperationException($"Cannot complete order. Current status: {Status}");
        }

        var cylinderDict = cylinders.ToDictionary(c => c.CylinderId);
        var allDelivered = _cylinders.Count > 0 &&
                           _cylinders.All(cylinderRef =>
                               cylinderDict.TryGetValue(cylinderRef.CylinderId, out var cylinder) &&
                               cylinder.State == CylinderState.Delivered);

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

    public void MarkAsShipped()
    {
        if (FulfillmentMethod != FulfillmentMethod.Shipping)
        {
            throw new InvalidOperationException("Cannot mark order as shipped when fulfillment method is pickup");
        }

        if (Status != RefillOrderStatus.Completed)
        {
            throw new InvalidOperationException($"Cannot mark order as shipped. Current status: {Status}");
        }

        ShippedAt ??= DateTime.UtcNow;
    }

    public bool NeedsNotification => Status == RefillOrderStatus.ReadyForPickup && NotifiedAt == null;

    public static bool IsCylinderReadyForPickup(CylinderState state)
    {
        return state is CylinderState.Ready or CylinderState.Problem or CylinderState.Delivered;
    }

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
