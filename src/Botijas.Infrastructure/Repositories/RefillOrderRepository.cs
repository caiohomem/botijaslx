using Botijas.Domain.Entities;
using Botijas.Domain.Repositories;
using Botijas.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Botijas.Infrastructure.Repositories;

public class RefillOrderRepository : IRefillOrderRepository
{
    private readonly BotijasDbContext _context;

    public RefillOrderRepository(BotijasDbContext context)
    {
        _context = context;
    }

    public async Task<RefillOrder?> FindByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.Cylinders)
            .FirstOrDefaultAsync(o => o.OrderId == orderId, cancellationToken);
    }

    public async Task<RefillOrder?> FindByCylinderIdAsync(Guid cylinderId, CancellationToken cancellationToken = default)
    {
        var orderId = await _context.CylinderRefs
            .Where(cr => cr.CylinderId == cylinderId)
            .Join(
                _context.Orders,
                cr => cr.OrderId,
                o => o.OrderId,
                (cr, o) => new { o.OrderId, o.Status, o.CreatedAt })
            .OrderBy(x => x.Status == RefillOrderStatus.Open ? 0 :
                          x.Status == RefillOrderStatus.ReadyForPickup ? 1 :
                          x.Status == RefillOrderStatus.Completed ? 2 : 3)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => x.OrderId)
            .FirstOrDefaultAsync(cancellationToken);

        if (orderId == Guid.Empty)
        {
            return null;
        }

        return await FindByIdAsync(orderId, cancellationToken);
    }

    public async Task<List<CurrentCylinderOrderInfo>> FindCurrentCylinderOrdersByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var links = await _context.CylinderRefs
            .Join(
                _context.Orders,
                cr => cr.OrderId,
                o => o.OrderId,
                (cr, o) => new { cr, o })
            .Where(x => x.o.CustomerId == customerId)
            .Select(x => new CurrentCylinderOrderInfo
                {
                    CylinderId = x.cr.CylinderId,
                    OrderId = x.o.OrderId,
                    OrderStatus = x.o.Status,
                    OrderCreatedAt = x.o.CreatedAt,
                    FulfillmentMethod = x.o.FulfillmentMethod,
                    RefillPaid = x.o.RefillPaid,
                    ShippingPaid = x.o.ShippingPaid,
                    CompletedAt = x.o.CompletedAt,
                    CancelledAt = x.o.CancelledAt,
                    CancellationNotes = x.o.CancellationNotes
                })
            .ToListAsync(cancellationToken);

        return links
            .GroupBy(x => x.CylinderId)
            .Select(g => g
                .OrderBy(x => x.OrderStatus == RefillOrderStatus.Open ? 0 :
                              x.OrderStatus == RefillOrderStatus.ReadyForPickup ? 1 :
                              x.OrderStatus == RefillOrderStatus.Completed ? 2 : 3)
                .ThenByDescending(x => x.OrderCreatedAt)
                .First())
            .ToList();
    }

    public async Task<List<RefillOrder>> FindOpenOrdersByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.Cylinders)
            .Where(o => o.CustomerId == customerId && o.Status == RefillOrderStatus.Open)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<RefillOrder>> FindAllByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.Cylinders)
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<RefillOrder>> FindReadyForPickupAsync(Guid? customerId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Orders
            .Include(o => o.Cylinders)
            .Where(o =>
                o.Status == RefillOrderStatus.ReadyForPickup ||
                (
                    o.Status == RefillOrderStatus.Open &&
                    o.Cylinders.Any() &&
                    o.Cylinders.All(cr => _context.Cylinders.Any(c =>
                        c.CylinderId == cr.CylinderId &&
                        (c.State == CylinderState.Ready ||
                         c.State == CylinderState.Problem ||
                         c.State == CylinderState.Delivered)))
                ));

        if (customerId.HasValue)
        {
            query = query.Where(o => o.CustomerId == customerId.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(RefillOrder order, CancellationToken cancellationToken = default)
    {
        await _context.Orders.AddAsync(order, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
