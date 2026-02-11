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
        // Buscar botijas em estado Received que estão em pedidos Open
        var query = from cylinder in _context.Cylinders
                    join cylinderRef in _context.CylinderRefs on cylinder.CylinderId equals cylinderRef.CylinderId
                    join order in _context.Orders on cylinderRef.OrderId equals order.OrderId
                    join customer in _context.Customers on order.CustomerId equals customer.CustomerId
                    where cylinder.State == CylinderState.Received && order.Status == RefillOrderStatus.Open
                    orderby cylinder.CreatedAt ascending
                    select new
                    {
                        Cylinder = cylinder,
                        Order = order,
                        Customer = customer
                    };

        var results = await query.ToListAsync(cancellationToken);

        // Agrupar por pedido para calcular progresso
        var orderGroups = results.GroupBy(r => r.Order.OrderId).ToDictionary(
            g => g.Key,
            g => new { TotalCount = g.Count(), Items = g.ToList() }
        );

        // Buscar contagem de botijas prontas por pedido
        var orderIds = orderGroups.Keys.ToList();
        var readyCounts = await _context.Cylinders
            .Join(_context.CylinderRefs, c => c.CylinderId, cr => cr.CylinderId, (c, cr) => new { c, cr })
            .Where(x => orderIds.Contains(x.cr.OrderId) && x.c.State == CylinderState.Ready)
            .GroupBy(x => x.cr.OrderId)
            .Select(g => new { OrderId = g.Key, ReadyCount = g.Count() })
            .ToDictionaryAsync(x => x.OrderId, x => x.ReadyCount, cancellationToken);

        // Buscar total de botijas por pedido
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
            TotalCylindersInOrder = totalCounts.GetValueOrDefault(r.Order.OrderId, 0),
            ReadyCylindersInOrder = readyCounts.GetValueOrDefault(r.Order.OrderId, 0)
        }).ToList();
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

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
