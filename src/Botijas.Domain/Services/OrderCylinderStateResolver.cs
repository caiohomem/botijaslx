using Botijas.Domain.Entities;

namespace Botijas.Domain.Services;

/// <summary>
/// Resolve o estado de uma botija no contexto de um pedido.
/// Pedidos ativos usam Cylinder.State; pedidos fechados usam o histórico daquele OrderId.
/// </summary>
public static class OrderCylinderStateResolver
{
    private static readonly CylinderEventType[] StateEvents =
    [
        CylinderEventType.Received,
        CylinderEventType.MarkedReady,
        CylinderEventType.Delivered,
        CylinderEventType.ProblemReported,
        CylinderEventType.OrderCancelled
    ];

    public static string Resolve(
        RefillOrderStatus orderStatus,
        CylinderState currentCylinderState,
        IEnumerable<CylinderHistoryEntry> cylinderHistory,
        Guid orderId)
    {
        if (orderStatus is RefillOrderStatus.Open or RefillOrderStatus.ReadyForPickup)
        {
            return currentCylinderState.ToString();
        }

        var stateFromHistory = TryResolveFromHistory(cylinderHistory, orderId);
        if (stateFromHistory.HasValue)
        {
            return stateFromHistory.Value.ToString();
        }

        // Pedidos fechados sem eventos com OrderId (dados legados): fim de ciclo esperado.
        return CylinderState.Delivered.ToString();
    }

    public static CylinderState? TryResolveFromHistory(
        IEnumerable<CylinderHistoryEntry> cylinderHistory,
        Guid orderId)
    {
        var latest = CylinderHistoryUndo.ActiveEvents(cylinderHistory)
            .Where(h => h.OrderId == orderId)
            .Where(h => StateEvents.Contains(h.EventType))
            .OrderByDescending(h => h.Timestamp)
            .ThenByDescending(h => h.Id)
            .FirstOrDefault();

        return latest?.EventType switch
        {
            CylinderEventType.Received => CylinderState.Received,
            CylinderEventType.MarkedReady => CylinderState.Ready,
            CylinderEventType.Delivered => CylinderState.Delivered,
            CylinderEventType.ProblemReported => CylinderState.Problem,
            CylinderEventType.OrderCancelled => CylinderState.Delivered,
            _ => null
        };
    }
}
