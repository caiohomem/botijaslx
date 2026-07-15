using Botijas.Domain.Entities;
using Botijas.Domain.Services;
using Xunit;

namespace Botijas.Domain.Tests;

public class OrderCylinderStateResolverTests
{
    [Fact]
    public void Resolve_ForOpenOrder_UsesCurrentCylinderState()
    {
        var orderId = Guid.NewGuid();
        var history = new[]
        {
            CylinderHistoryEntry.Create(Guid.NewGuid(), CylinderEventType.Delivered, orderId: orderId)
        };

        var result = OrderCylinderStateResolver.Resolve(
            RefillOrderStatus.Open,
            CylinderState.Received,
            history,
            orderId);

        Assert.Equal("Received", result);
    }

    [Fact]
    public void Resolve_ForReadyForPickup_UsesCurrentCylinderState()
    {
        var orderId = Guid.NewGuid();

        var result = OrderCylinderStateResolver.Resolve(
            RefillOrderStatus.ReadyForPickup,
            CylinderState.Ready,
            Array.Empty<CylinderHistoryEntry>(),
            orderId);

        Assert.Equal("Ready", result);
    }

    [Fact]
    public void Resolve_ForCompletedOrder_UsesLastHistoryEventForThatOrder()
    {
        var cylinderId = Guid.NewGuid();
        var oldOrderId = Guid.NewGuid();
        var newOrderId = Guid.NewGuid();

        var history = new[]
        {
            CylinderHistoryEntry.Create(cylinderId, CylinderEventType.Received, orderId: oldOrderId),
            CylinderHistoryEntry.Create(cylinderId, CylinderEventType.MarkedReady, orderId: oldOrderId),
            CylinderHistoryEntry.Create(cylinderId, CylinderEventType.Delivered, orderId: oldOrderId),
            // Ciclo novo — não deve afetar o pedido antigo
            CylinderHistoryEntry.Create(cylinderId, CylinderEventType.Received, orderId: newOrderId),
            CylinderHistoryEntry.Create(cylinderId, CylinderEventType.MarkedReady, orderId: newOrderId),
        };

        var result = OrderCylinderStateResolver.Resolve(
            RefillOrderStatus.Completed,
            CylinderState.Ready, // estado atual = ciclo novo
            history,
            oldOrderId);

        Assert.Equal("Delivered", result);
    }

    [Fact]
    public void Resolve_ForCancelledOrder_MapsOrderCancelledToDelivered()
    {
        var cylinderId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var history = new[]
        {
            CylinderHistoryEntry.Create(cylinderId, CylinderEventType.Received, orderId: orderId),
            CylinderHistoryEntry.Create(cylinderId, CylinderEventType.OrderCancelled, orderId: orderId),
        };

        var result = OrderCylinderStateResolver.Resolve(
            RefillOrderStatus.Cancelled,
            CylinderState.Received,
            history,
            orderId);

        Assert.Equal("Delivered", result);
    }

    [Fact]
    public void Resolve_IgnoresUndoneMarkedReady()
    {
        var cylinderId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var markedReady = CylinderHistoryEntry.Create(cylinderId, CylinderEventType.MarkedReady, orderId: orderId);
        var received = CylinderHistoryEntry.Create(cylinderId, CylinderEventType.Received, orderId: orderId);
        var undo = CylinderHistoryEntry.Create(
            cylinderId,
            CylinderEventType.ActionUndone,
            details: $"Ação desfeita: MarkedReady. ||UNDO:{markedReady.Id}||",
            orderId: orderId);

        // Ordem temporal: Received → MarkedReady → Undo
        var history = new[] { received, markedReady, undo };

        var result = OrderCylinderStateResolver.Resolve(
            RefillOrderStatus.Completed,
            CylinderState.Ready,
            history,
            orderId);

        Assert.Equal("Received", result);
    }

    [Fact]
    public void Resolve_CompletedWithoutOrderHistory_FallsBackToDelivered()
    {
        var result = OrderCylinderStateResolver.Resolve(
            RefillOrderStatus.Completed,
            CylinderState.Received,
            Array.Empty<CylinderHistoryEntry>(),
            Guid.NewGuid());

        Assert.Equal("Delivered", result);
    }

    [Theory]
    [InlineData(CylinderEventType.Received, CylinderState.Received)]
    [InlineData(CylinderEventType.MarkedReady, CylinderState.Ready)]
    [InlineData(CylinderEventType.Delivered, CylinderState.Delivered)]
    [InlineData(CylinderEventType.ProblemReported, CylinderState.Problem)]
    [InlineData(CylinderEventType.OrderCancelled, CylinderState.Delivered)]
    public void TryResolveFromHistory_MapsEvents(CylinderEventType eventType, CylinderState expected)
    {
        var orderId = Guid.NewGuid();
        var history = new[]
        {
            CylinderHistoryEntry.Create(Guid.NewGuid(), eventType, orderId: orderId)
        };

        var result = OrderCylinderStateResolver.TryResolveFromHistory(history, orderId);

        Assert.Equal(expected, result);
    }
}
