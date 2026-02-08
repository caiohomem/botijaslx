using Botijas.Application.Reports.Queries;
using Botijas.Domain.Entities;
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

    public async Task<DashboardStatsDto> GetStatsAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var weekAgo = today.AddDays(-7);

        // Contagens de pedidos
        var ordersOpen = await _context.Orders
            .CountAsync(o => o.Status == RefillOrderStatus.Open, cancellationToken);

        var ordersReadyForPickup = await _context.Orders
            .CountAsync(o => o.Status == RefillOrderStatus.ReadyForPickup, cancellationToken);

        var ordersCompletedToday = await _context.Orders
            .CountAsync(o => o.Status == RefillOrderStatus.Completed && 
                            o.CompletedAt != null && 
                            o.CompletedAt.Value.Date == today, cancellationToken);

        var ordersCompletedThisWeek = await _context.Orders
            .CountAsync(o => o.Status == RefillOrderStatus.Completed && 
                            o.CompletedAt != null && 
                            o.CompletedAt.Value.Date >= weekAgo, cancellationToken);

        // Contagens de botijas
        var cylindersReceived = await _context.Cylinders
            .CountAsync(c => c.State == CylinderState.Received, cancellationToken);

        var cylindersReady = await _context.Cylinders
            .CountAsync(c => c.State == CylinderState.Ready, cancellationToken);

        var cylindersWithProblem = await _context.Cylinders
            .CountAsync(c => c.State == CylinderState.Problem, cancellationToken);

        var cylindersFilledToday = await _context.CylinderHistory
            .CountAsync(h => h.EventType == CylinderEventType.MarkedReady && 
                            h.Timestamp.Date == today, cancellationToken);

        var cylindersFilledThisWeek = await _context.CylinderHistory
            .CountAsync(h => h.EventType == CylinderEventType.MarkedReady && 
                            h.Timestamp.Date >= weekAgo, cancellationToken);

        // Total de clientes
        var totalCustomers = await _context.Customers.CountAsync(cancellationToken);

        // Pedidos aguardando notificação
        var ordersAwaitingNotification = await _context.Orders
            .CountAsync(o => o.Status == RefillOrderStatus.ReadyForPickup && 
                            o.NotifiedAt == null, cancellationToken);

        return new DashboardStatsDto
        {
            OrdersOpen = ordersOpen,
            OrdersReadyForPickup = ordersReadyForPickup,
            OrdersCompletedToday = ordersCompletedToday,
            OrdersCompletedThisWeek = ordersCompletedThisWeek,
            OrdersAwaitingNotification = ordersAwaitingNotification,
            CylindersReceived = cylindersReceived,
            CylindersReady = cylindersReady,
            CylindersWithProblem = cylindersWithProblem,
            CylindersFilledToday = cylindersFilledToday,
            CylindersFilledThisWeek = cylindersFilledThisWeek,
            TotalCustomers = totalCustomers
        };
    }
}
