using Botijas.Application.Common;
using Botijas.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Botijas.Api.Handlers;

public class DeleteCylinderHandler
{
    private readonly BotijasDbContext _context;

    public DeleteCylinderHandler(BotijasDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(Guid cylinderId, CancellationToken cancellationToken)
    {
        var cylinder = await _context.Cylinders
            .FirstOrDefaultAsync(c => c.CylinderId == cylinderId, cancellationToken);

        if (cylinder == null)
            return Result.Failure("Botija não encontrada");

        // 1. Delete CylinderHistoryEntry rows (no cascade configured)
        var historyEntries = await _context.CylinderHistory
            .Where(h => h.CylinderId == cylinderId)
            .ToListAsync(cancellationToken);
        _context.CylinderHistory.RemoveRange(historyEntries);

        // 2. Delete CylinderRef rows (no cascade configured from Cylinder side)
        var cylinderRefs = await _context.CylinderRefs
            .Where(cr => cr.CylinderId == cylinderId)
            .ToListAsync(cancellationToken);
        _context.CylinderRefs.RemoveRange(cylinderRefs);

        // 3. Delete Cylinder
        _context.Cylinders.Remove(cylinder);

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
