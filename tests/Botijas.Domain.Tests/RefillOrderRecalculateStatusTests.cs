using Botijas.Domain.Entities;
using Xunit;

namespace Botijas.Domain.Tests;

public class RefillOrderRecalculateStatusTests
{
    [Fact]
    public void RecalculateStatus_WhenLastCylinderBecomesReady_MovesOrderToReadyForPickup()
    {
        var (order, cylinders) = CreateOpenOrderWithCylinders(2);
        cylinders[0].MarkAsReady();

        order.RecalculateStatus(cylinders);
        Assert.Equal(RefillOrderStatus.Open, order.Status);

        cylinders[1].MarkAsReady();
        order.RecalculateStatus(cylinders);

        Assert.Equal(RefillOrderStatus.ReadyForPickup, order.Status);
        Assert.Contains(order.DomainEvents, e => e.GetType().Name == "OrderBecameReadyForPickup");
    }

    [Fact]
    public void RecalculateStatus_WhenAllDelivered_CompletesOrder()
    {
        var (order, cylinders) = CreateOpenOrderWithCylinders(1);
        cylinders[0].MarkAsReady();
        order.RecalculateStatus(cylinders);
        Assert.Equal(RefillOrderStatus.ReadyForPickup, order.Status);

        cylinders[0].MarkAsDelivered();
        order.RecalculateStatus(cylinders);

        Assert.Equal(RefillOrderStatus.Completed, order.Status);
        Assert.NotNull(order.CompletedAt);
    }

    [Fact]
    public void RecalculateStatus_WhenUndoMakesCylinderReceivedAgain_ReopensOrder()
    {
        var (order, cylinders) = CreateOpenOrderWithCylinders(1);
        cylinders[0].MarkAsReady();
        order.RecalculateStatus(cylinders);
        order.MarkAsNotified();
        Assert.NotNull(order.NotifiedAt);

        cylinders[0].RevertToReceived();
        order.RecalculateStatus(cylinders);

        Assert.Equal(RefillOrderStatus.Open, order.Status);
        Assert.Null(order.NotifiedAt);
    }

    [Fact]
    public void RecalculateStatus_OnCompletedOrder_DoesNotReopenWhenCylinderReused()
    {
        var (order, cylinders) = CreateOpenOrderWithCylinders(1);
        cylinders[0].MarkAsReady();
        cylinders[0].MarkAsDelivered();
        order.RecalculateStatus(cylinders);
        Assert.Equal(RefillOrderStatus.Completed, order.Status);
        var completedAt = order.CompletedAt;

        // Simula reutilização: botija entra noutro ciclo (Received) mas pedido antigo permanece Completed.
        cylinders[0].ReceiveForRefill();
        order.RecalculateStatus(cylinders);

        Assert.Equal(RefillOrderStatus.Completed, order.Status);
        Assert.Equal(completedAt, order.CompletedAt);
    }

    [Fact]
    public void RecalculateStatus_OnCancelledOrder_DoesNotChangeStatus()
    {
        var (order, cylinders) = CreateOpenOrderWithCylinders(1);
        order.Cancel("teste");
        Assert.Equal(RefillOrderStatus.Cancelled, order.Status);

        cylinders[0].MarkAsReady();
        order.RecalculateStatus(cylinders);

        Assert.Equal(RefillOrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void RecalculateStatus_UsesCylinderEntityState_EvenIfMembershipHasNoState()
    {
        var (order, cylinders) = CreateOpenOrderWithCylinders(1);

        cylinders[0].MarkAsReady();
        Assert.Equal(CylinderState.Ready, cylinders[0].State);
        Assert.Single(order.Cylinders);

        order.RecalculateStatus(cylinders);

        Assert.Equal(RefillOrderStatus.ReadyForPickup, order.Status);
    }

    [Fact]
    public void Complete_RequiresAllCylindersDeliveredFromEntities()
    {
        var (order, cylinders) = CreateOpenOrderWithCylinders(1);
        cylinders[0].MarkAsReady();
        order.RecalculateStatus(cylinders);

        Assert.Throws<InvalidOperationException>(() => order.Complete(cylinders));

        cylinders[0].MarkAsDelivered();
        order.Complete(cylinders);

        Assert.Equal(RefillOrderStatus.Completed, order.Status);
    }

    private static (RefillOrder Order, List<Cylinder> Cylinders) CreateOpenOrderWithCylinders(int count)
    {
        var customerId = Guid.NewGuid();
        var order = RefillOrder.Create(customerId);
        var cylinders = new List<Cylinder>();

        for (var i = 0; i < count; i++)
        {
            var cylinder = Cylinder.Create(i + 1);
            order.AddCylinder(cylinder);
            cylinders.Add(cylinder);
        }

        return (order, cylinders);
    }
}
