using Botijas.Application.Reports.Queries;
using Botijas.Domain.Entities;
using Botijas.Domain.Services;
using Botijas.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Botijas.Infrastructure.Queries;

public class DashboardStatsQuery : IDashboardStatsQuery
{
    private readonly BotijasDbContext _context;

    public DashboardStatsQuery(BotijasDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardStatsDto> GetStatsAsync(int days, CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var weekStart = today.AddDays(-6);
        var seriesStart = today.AddDays(-(days - 1));
        // Inclui ActionUndone um pouco antes do período para anular eventos no intervalo.
        var historyLoadStart = seriesStart.AddDays(-30);

        // Contagens de pedidos
        var ordersOpen = await _context.Orders
            .CountAsync(o => o.Status == RefillOrderStatus.Open, cancellationToken);

        var ordersReadyForPickup = await _context.Orders
            .CountAsync(o => o.Status == RefillOrderStatus.ReadyForPickup &&
                            o.FulfillmentMethod == FulfillmentMethod.Pickup, cancellationToken);

        var ordersReadyForShipping = await _context.Orders
            .CountAsync(o => o.Status == RefillOrderStatus.ReadyForPickup &&
                            o.FulfillmentMethod == FulfillmentMethod.Shipping, cancellationToken);

        var ordersCompletedToday = await _context.Orders
            .CountAsync(o => o.Status == RefillOrderStatus.Completed &&
                            o.CompletedAt != null &&
                            o.CompletedAt.Value.Date == today, cancellationToken);

        var ordersCompletedThisWeek = await _context.Orders
            .CountAsync(o => o.Status == RefillOrderStatus.Completed &&
                            o.CompletedAt != null &&
                            o.CompletedAt.Value.Date >= weekStart, cancellationToken);

        // Contagens de botijas
        var cylindersReceived = await _context.Cylinders
            .CountAsync(c => c.State == CylinderState.Received, cancellationToken);

        var cylindersReady = await _context.Cylinders
            .CountAsync(c => c.State == CylinderState.Ready, cancellationToken);

        var cylindersWithProblem = await _context.Cylinders
            .CountAsync(c => c.State == CylinderState.Problem, cancellationToken);

        var historyRaw = await _context.CylinderHistory
            .AsNoTracking()
            .Where(h =>
                (h.EventType == CylinderEventType.Received ||
                 h.EventType == CylinderEventType.MarkedReady ||
                 h.EventType == CylinderEventType.Delivered ||
                 h.EventType == CylinderEventType.ActionUndone) &&
                h.Timestamp.Date >= historyLoadStart)
            .Select(h => new { h.Id, h.EventType, h.Timestamp, h.Details })
            .ToListAsync(cancellationToken);

        var undoneIds = CylinderHistoryUndo.ExtractUndoneIds(
            historyRaw.Select(h => (h.EventType, h.Details)));

        var activeHistory = historyRaw
            .Where(h =>
                h.EventType != CylinderEventType.ActionUndone &&
                !undoneIds.Contains(h.Id))
            .ToList();

        var cylindersFilledToday = activeHistory.Count(h =>
            h.EventType == CylinderEventType.MarkedReady && h.Timestamp.Date == today);

        var cylindersReceivedToday = activeHistory.Count(h =>
            h.EventType == CylinderEventType.Received && h.Timestamp.Date == today);

        var cylindersFilledThisWeek = activeHistory.Count(h =>
            h.EventType == CylinderEventType.MarkedReady && h.Timestamp.Date >= weekStart);

        var dailySeriesRaw = activeHistory
            .Where(h =>
                (h.EventType == CylinderEventType.Received ||
                 h.EventType == CylinderEventType.MarkedReady ||
                 h.EventType == CylinderEventType.Delivered) &&
                h.Timestamp.Date >= seriesStart)
            .GroupBy(h => h.Timestamp.Date)
            .Select(g => new
            {
                Date = g.Key,
                Received = g.Count(h => h.EventType == CylinderEventType.Received),
                Ready = g.Count(h => h.EventType == CylinderEventType.MarkedReady),
                Delivered = g.Count(h => h.EventType == CylinderEventType.Delivered)
            })
            .ToList();

        var dailySeries = Enumerable.Range(0, days)
            .Select(offset => seriesStart.AddDays(offset))
            .Select(day =>
            {
                var match = dailySeriesRaw.FirstOrDefault(x => x.Date == day);

                return new DashboardDailySeriesPointDto
                {
                    Date = day.ToString("yyyy-MM-dd"),
                    Received = match?.Received ?? 0,
                    Ready = match?.Ready ?? 0,
                    Delivered = match?.Delivered ?? 0
                };
            })
            .ToList();

        // Total de clientes
        var totalCustomers = await _context.Customers.CountAsync(cancellationToken);

        // Pedidos aguardando notificação (por canal)
        var ordersAwaitingNotificationPickup = await _context.Orders
            .CountAsync(o => o.Status == RefillOrderStatus.ReadyForPickup &&
                            o.FulfillmentMethod == FulfillmentMethod.Pickup &&
                            o.NotifiedAt == null, cancellationToken);

        var ordersAwaitingNotificationShipping = await _context.Orders
            .CountAsync(o => o.Status == RefillOrderStatus.ReadyForPickup &&
                            o.FulfillmentMethod == FulfillmentMethod.Shipping &&
                            o.NotifiedAt == null, cancellationToken);

        return new DashboardStatsDto
        {
            OrdersOpen = ordersOpen,
            OrdersReadyForPickup = ordersReadyForPickup,
            OrdersReadyForShipping = ordersReadyForShipping,
            OrdersCompletedToday = ordersCompletedToday,
            OrdersCompletedThisWeek = ordersCompletedThisWeek,
            OrdersAwaitingNotification = ordersAwaitingNotificationPickup + ordersAwaitingNotificationShipping,
            OrdersAwaitingNotificationPickup = ordersAwaitingNotificationPickup,
            OrdersAwaitingNotificationShipping = ordersAwaitingNotificationShipping,
            CylindersReceived = cylindersReceived,
            CylindersReady = cylindersReady,
            CylindersWithProblem = cylindersWithProblem,
            CylindersFilledToday = cylindersFilledToday,
            CylindersReceivedToday = cylindersReceivedToday,
            CylindersFilledThisWeek = cylindersFilledThisWeek,
            TotalCustomers = totalCustomers,
            DailySeries = dailySeries
        };
    }
}
