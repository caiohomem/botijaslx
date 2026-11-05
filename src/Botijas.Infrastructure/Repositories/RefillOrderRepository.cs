using System.Data;
using Botijas.Domain.Entities;
using Botijas.Domain.Repositories;
using Botijas.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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

    public async Task<Dictionary<Guid, DateTime>> GetReadyAtByOrderAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _context.CylinderHistory
            .Where(h => h.EventType == CylinderEventType.MarkedReady && h.OrderId != null)
            .GroupBy(h => h.OrderId!.Value)
            .Select(g => new { OrderId = g.Key, ReadyAt = g.Max(h => h.Timestamp) })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(x => x.OrderId, x => x.ReadyAt);
    }

    public async Task AddAsync(RefillOrder order, CancellationToken cancellationToken = default)
    {
        await _context.Orders.AddAsync(order, cancellationToken);
    }

    public Task DeleteAsync(RefillOrder order, CancellationToken cancellationToken = default)
    {
        _context.Orders.Remove(order);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ExecuteLockedAsync(
        Guid orderId,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction.GetDbTransaction();
            command.CommandText = """SELECT 1 FROM "Orders" WHERE "OrderId" = @id FOR UPDATE""";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "id";
            parameter.Value = orderId;
            command.Parameters.Add(parameter);
            await command.ExecuteScalarAsync(cancellationToken);
        }

        // Leituras feitas antes do lock ficam obsoletas. A acção tem de voltar a carregar.
        foreach (var entry in _context.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is RefillOrder or Cylinder or CylinderRef)
            {
                entry.State = EntityState.Detached;
            }
        }

        await action(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
