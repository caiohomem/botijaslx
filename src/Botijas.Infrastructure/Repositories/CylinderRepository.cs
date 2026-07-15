using Botijas.Domain.Entities;
using Botijas.Domain.Repositories;
using Botijas.Domain.ValueObjects;
using Botijas.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Botijas.Infrastructure.Repositories;

public class CylinderRepository : ICylinderRepository
{
    private readonly BotijasDbContext _context;

    public CylinderRepository(BotijasDbContext context)
    {
        _context = context;
    }

    public async Task<Cylinder?> FindByIdAsync(Guid cylinderId, CancellationToken cancellationToken = default)
    {
        return await _context.Cylinders
            .FirstOrDefaultAsync(c => c.CylinderId == cylinderId, cancellationToken);
    }

    public async Task<Cylinder?> FindByLabelTokenAsync(LabelToken labelToken, CancellationToken cancellationToken = default)
    {
        // EF Core armazena LabelToken como string, então carregamos e comparamos em memória
        var labelValue = labelToken.Value;
        var cylinders = await _context.Cylinders.ToListAsync(cancellationToken);
        return cylinders.FirstOrDefault(c => c.LabelToken != null && c.LabelToken.Value == labelValue);
    }

    public async Task<Cylinder?> FindBySequentialNumberAsync(long sequentialNumber, CancellationToken cancellationToken = default)
    {
        return await _context.Cylinders
            .FirstOrDefaultAsync(c => c.SequentialNumber == sequentialNumber, cancellationToken);
    }

    public async Task<Cylinder?> FindInOpenOrderAsync(Guid cylinderId, CancellationToken cancellationToken = default)
    {
        // Verificar se o cilindro está em algum pedido aberto
        var cylinderRef = await _context.CylinderRefs
            .Join(_context.Orders,
                cr => cr.OrderId,
                o => o.OrderId,
                (cr, o) => new { CylinderRef = cr, Order = o })
            .Where(x => x.CylinderRef.CylinderId == cylinderId && x.Order.Status == RefillOrderStatus.Open)
            .Select(x => x.CylinderRef)
            .FirstOrDefaultAsync(cancellationToken);

        if (cylinderRef == null)
        {
            return null;
        }

        return await FindByIdAsync(cylinderId, cancellationToken);
    }

    public async Task<List<Cylinder>> FindByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var cylinderIds = await _context.CylinderRefs
            .Where(cr => cr.OrderId == orderId)
            .Select(cr => cr.CylinderId)
            .ToListAsync(cancellationToken);

        return await _context.Cylinders
            .Where(c => cylinderIds.Contains(c.CylinderId))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<FillingQueueItem>> GetFillingQueueAsync(CancellationToken cancellationToken = default)
    {
        // Fonte de verdade: Cylinder.State + Order.Status (não CylinderRef.State).
        var query = from cylinderRef in _context.CylinderRefs
                    join cylinder in _context.Cylinders on cylinderRef.CylinderId equals cylinder.CylinderId
                    join order in _context.Orders on cylinderRef.OrderId equals order.OrderId
                    join customer in _context.Customers on order.CustomerId equals customer.CustomerId
                    where cylinder.State == CylinderState.Received
                          && order.Status == RefillOrderStatus.Open
                    orderby cylinder.CreatedAt ascending, cylinder.SequentialNumber ascending
                    select new
                    {
                        Cylinder = cylinder,
                        Order = order,
                        Customer = customer
                    };

        var results = await query.ToListAsync(cancellationToken);

        var orderIds = results.Select(r => r.Order.OrderId).Distinct().ToList();

        var readyCounts = await (
            from cr in _context.CylinderRefs
            join c in _context.Cylinders on cr.CylinderId equals c.CylinderId
            where orderIds.Contains(cr.OrderId) &&
                  (c.State == CylinderState.Ready ||
                   c.State == CylinderState.Problem ||
                   c.State == CylinderState.Delivered)
            group c by cr.OrderId into g
            select new { OrderId = g.Key, ReadyCount = g.Count() }
        ).ToDictionaryAsync(x => x.OrderId, x => x.ReadyCount, cancellationToken);

        var totalCounts = await _context.CylinderRefs
            .Where(cr => orderIds.Contains(cr.OrderId))
            .GroupBy(cr => cr.OrderId)
            .Select(g => new { OrderId = g.Key, TotalCount = g.Count() })
            .ToDictionaryAsync(x => x.OrderId, x => x.TotalCount, cancellationToken);

        return results.Select(r => new FillingQueueItem
            {
                CylinderId = r.Cylinder.CylinderId,
                SequentialNumber = r.Cylinder.SequentialNumber,
                LabelToken = r.Cylinder.LabelToken?.Value,
                State = r.Cylinder.State.ToString(),
                ReceivedAt = r.Cylinder.CreatedAt,
                OrderId = r.Order.OrderId,
                CustomerName = r.Customer.Name,
                CustomerPhone = r.Customer.Phone.Value,
                CustomerPhoneType = r.Customer.PhoneType.ToString(),
                FulfillmentMethod = r.Order.FulfillmentMethod.ToString(),
                TotalCylindersInOrder = totalCounts.GetValueOrDefault(r.Order.OrderId, 0),
                ReadyCylindersInOrder = readyCounts.GetValueOrDefault(r.Order.OrderId, 0)
        }).ToList();
    }

    public async Task<List<Cylinder>> FindByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var cylinderIds = await _context.CylinderRefs
            .Join(_context.Orders,
                cr => cr.OrderId,
                o => o.OrderId,
                (cr, o) => new { cr, o })
            .Where(x => x.o.CustomerId == customerId)
            .Select(x => x.cr.CylinderId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return await _context.Cylinders
            .Where(c => cylinderIds.Contains(c.CylinderId))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ProblemCylinderItem>> GetProblemCylindersAsync(CancellationToken cancellationToken = default)
    {
        var cylinders = await _context.Cylinders
            .Where(c => c.State == CylinderState.Problem)
            .Select(c => new ProblemCylinderItem
            {
                CylinderId = c.CylinderId,
                SequentialNumber = c.SequentialNumber,
                LabelToken = c.LabelToken != null ? c.LabelToken.Value : null,
                State = c.State.ToString(),
                OccurrenceNotes = c.OccurrenceNotes,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var linkRows = await (
            from cylinderRef in _context.CylinderRefs
            join order in _context.Orders on cylinderRef.OrderId equals order.OrderId
            join customer in _context.Customers on order.CustomerId equals customer.CustomerId
            select new
            {
                cylinderRef.CylinderId,
                order.OrderId,
                OrderStatus = order.Status,
                order.CreatedAt,
                CustomerId = customer.CustomerId,
                CustomerName = customer.Name,
                CustomerPhone = customer.Phone.Value,
                CustomerPhoneType = customer.PhoneType
            })
            .ToListAsync(cancellationToken);

        var linkByCylinderId = linkRows
            .GroupBy(x => x.CylinderId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(x => x.OrderStatus == RefillOrderStatus.Open ? 0 :
                                     x.OrderStatus == RefillOrderStatus.ReadyForPickup ? 1 : 2)
                      .ThenByDescending(x => x.CreatedAt)
                      .First());

        foreach (var cylinder in cylinders)
        {
            if (!linkByCylinderId.TryGetValue(cylinder.CylinderId, out var link))
            {
                continue;
            }

            cylinder.OrderId = link.OrderId;
            cylinder.OrderStatus = link.OrderStatus.ToString();
            cylinder.CustomerId = link.CustomerId;
            cylinder.CustomerName = link.CustomerName;
            cylinder.CustomerPhone = link.CustomerPhone;
            cylinder.CustomerPhoneType = link.CustomerPhoneType.ToString();
        }

        return cylinders
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.SequentialNumber)
            .ToList();
    }

    public async Task AddAsync(Cylinder cylinder, CancellationToken cancellationToken = default)
    {
        if (cylinder.SequentialNumber == 0)
        {
            // Max from database
            var maxDb = await _context.Cylinders
                .AsNoTracking()
                .MaxAsync(c => (long?)c.SequentialNumber, cancellationToken) ?? 0;

            // Max from locally tracked (not yet saved) cylinders
            var maxLocal = _context.ChangeTracker.Entries<Cylinder>()
                .Select(e => e.Entity.SequentialNumber)
                .DefaultIfEmpty(0)
                .Max();

            var nextSequential = Math.Max(maxDb, maxLocal) + 1;

            var property = typeof(Cylinder).GetProperty("SequentialNumber");
            property?.SetValue(cylinder, nextSequential);
        }

        await _context.Cylinders.AddAsync(cylinder, cancellationToken);
    }

    public async Task DeleteAsync(Cylinder cylinder, CancellationToken cancellationToken = default)
    {
        _context.Cylinders.Remove(cylinder);
        await Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
