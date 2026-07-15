using Botijas.Domain.Entities;

namespace Botijas.Domain.Services;

/// <summary>
/// Resolve o estado de uma botija no contexto de um pedido.
/// Pedidos ativos usam Cylinder.State; pedidos fechados usam o histórico daquele OrderId.
/// </summary>
public static class OrderCylinderStateResolver
{
    private const string UndoMarker = "||UNDO:";

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
        var entries = cylinderHistory.ToList();
        var undoneIds = ExtractUndoneIds(entries);

        var latest = entries
            .Where(h => h.OrderId == orderId)
            .Where(h => h.EventType != CylinderEventType.ActionUndone)
            .Where(h => !undoneIds.Contains(h.Id))
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

    private static HashSet<Guid> ExtractUndoneIds(IEnumerable<CylinderHistoryEntry> history)
    {
        var undoneIds = new HashSet<Guid>();

        foreach (var entry in history.Where(h => h.EventType == CylinderEventType.ActionUndone))
        {
            var details = entry.Details ?? string.Empty;
            var start = details.IndexOf(UndoMarker, StringComparison.Ordinal);
            if (start < 0)
            {
                continue;
            }

            start += UndoMarker.Length;
            var end = details.IndexOf("||", start, StringComparison.Ordinal);
            if (end < 0)
            {
                continue;
            }

            var value = details[start..end];
            if (Guid.TryParse(value, out var undoneId))
            {
                undoneIds.Add(undoneId);
            }
        }

        return undoneIds;
    }
}
