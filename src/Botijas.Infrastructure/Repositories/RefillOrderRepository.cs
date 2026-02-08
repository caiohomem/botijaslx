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
            .Select(cr => cr.OrderId)
            .FirstOrDefaultAsync(cancellationToken);

        if (orderId == Guid.Empty)
        {
            return null;
        }

        return await FindByIdAsync(orderId, cancellationToken);
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
            .Where(o => o.Status == RefillOrderStatus.ReadyForPickup);

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
