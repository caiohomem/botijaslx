using Botijas.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Botijas.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DebugController : ControllerBase
{
    private readonly BotijasDbContext _dbContext;

    public DebugController(BotijasDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportDatabase(CancellationToken cancellationToken)
    {
        var unavailable = await EnsureDebugEnabled(cancellationToken);
        if (unavailable is not null) return unavailable;

        var snapshot = await BuildDatabaseExport(cancellationToken);
        return Ok(snapshot);
    }

    [HttpPost("import")]
    public async Task<IActionResult> ImportDatabase([FromBody] DebugDatabaseExport snapshot, CancellationToken cancellationToken)
    {
        var unavailable = await EnsureDebugEnabled(cancellationToken);
        if (unavailable is not null) return unavailable;

        if (snapshot.Customers is null ||
            snapshot.Orders is null ||
            snapshot.CylinderRefs is null ||
            snapshot.Cylinders is null ||
            snapshot.CylinderHistory is null ||
            snapshot.AppSettings is null)
        {
            return BadRequest(new { error = "Snapshot JSON inválido" });
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await _dbContext.Database.ExecuteSqlRawAsync("""DELETE FROM "CylinderHistory";""", cancellationToken);
            await _dbContext.Database.ExecuteSqlRawAsync("""DELETE FROM "CylinderRefs";""", cancellationToken);
            await _dbContext.Database.ExecuteSqlRawAsync("""DELETE FROM "Cylinders";""", cancellationToken);
            await _dbContext.Database.ExecuteSqlRawAsync("""DELETE FROM "Orders";""", cancellationToken);
            await _dbContext.Database.ExecuteSqlRawAsync("""DELETE FROM "Customers";""", cancellationToken);
            await _dbContext.Database.ExecuteSqlRawAsync("""DELETE FROM "AppSettings";""", cancellationToken);

            foreach (var customer in snapshot.Customers)
            {
                await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""INSERT INTO "Customers" ("CustomerId", "Name", "Phone", "PhoneType", "CreatedAt") VALUES ({customer.CustomerId}, {customer.Name}, {customer.Phone}, {customer.PhoneType}, {customer.CreatedAt});""",
                    cancellationToken);
            }

            foreach (var order in snapshot.Orders)
            {
                await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""INSERT INTO "Orders" ("OrderId", "CustomerId", "Status", "CreatedAt", "CompletedAt", "NotifiedAt") VALUES ({order.OrderId}, {order.CustomerId}, {order.Status}, {order.CreatedAt}, {order.CompletedAt}, {order.NotifiedAt});""",
                    cancellationToken);
            }

            foreach (var cylinder in snapshot.Cylinders)
            {
                await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""INSERT INTO "Cylinders" ("CylinderId", "SequentialNumber", "LabelToken", "State", "OccurrenceNotes", "CreatedAt") VALUES ({cylinder.CylinderId}, {cylinder.SequentialNumber}, {cylinder.LabelToken}, {cylinder.State}, {cylinder.OccurrenceNotes}, {cylinder.CreatedAt});""",
                    cancellationToken);
            }

            foreach (var cylinderRef in snapshot.CylinderRefs)
            {
                await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""INSERT INTO "CylinderRefs" ("OrderId", "CylinderId", "State") VALUES ({cylinderRef.OrderId}, {cylinderRef.CylinderId}, {cylinderRef.State});""",
                    cancellationToken);
            }

            foreach (var historyEntry in snapshot.CylinderHistory)
            {
                await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""INSERT INTO "CylinderHistory" ("Id", "CylinderId", "EventType", "Details", "OrderId", "Timestamp") VALUES ({historyEntry.Id}, {historyEntry.CylinderId}, {historyEntry.EventType}, {historyEntry.Details}, {historyEntry.OrderId}, {historyEntry.Timestamp});""",
                    cancellationToken);
            }

            foreach (var appSettings in snapshot.AppSettings)
            {
                await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""INSERT INTO "AppSettings" ("AppSettingsId", "StoreName", "StorePhone", "StoreLink", "AppTitle", "WhatsAppMessageTemplate", "WelcomeMessageTemplate", "ThankYouMessageTemplate", "PrinterType", "LabelWidthMm", "LabelHeightMm", "DebugEnabled", "SoundNotificationsDisabled", "UpdatedAt") VALUES ({appSettings.AppSettingsId}, {appSettings.StoreName}, {appSettings.StorePhone}, {appSettings.StoreLink}, {appSettings.AppTitle}, {appSettings.WhatsAppMessageTemplate}, {appSettings.WelcomeMessageTemplate}, {appSettings.ThankYouMessageTemplate}, {appSettings.PrinterType}, {appSettings.LabelWidthMm}, {appSettings.LabelHeightMm}, {appSettings.DebugEnabled}, {appSettings.SoundNotificationsDisabled}, {appSettings.UpdatedAt});""",
                    cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            return Ok(new
            {
                importedAt = DateTime.UtcNow,
                customers = snapshot.Customers.Count,
                orders = snapshot.Orders.Count,
                cylinders = snapshot.Cylinders.Count
            });
        }
        catch (PostgresException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return BadRequest(new { error = $"Falha ao importar snapshot: {ex.MessageText}" });
        }
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetFullSnapshot(CancellationToken cancellationToken)
    {
        var unavailable = await EnsureDebugEnabled(cancellationToken);
        if (unavailable is not null) return unavailable;

        var customers = await _dbContext.Customers
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new
            {
                c.CustomerId,
                c.Name,
                Phone = c.Phone.Value,
                PhoneType = c.PhoneType.ToString(),
                c.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var orders = await _dbContext.Orders
            .AsNoTracking()
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new
            {
                o.OrderId,
                o.CustomerId,
                Status = o.Status.ToString(),
                o.CreatedAt,
                o.CompletedAt,
                o.NotifiedAt
            })
            .ToListAsync(cancellationToken);

        var cylinderRefs = await _dbContext.CylinderRefs
            .AsNoTracking()
            .Select(cr => new
            {
                cr.OrderId,
                cr.CylinderId,
                State = cr.State.ToString()
            })
            .ToListAsync(cancellationToken);

        var cylinders = await _dbContext.Cylinders
            .AsNoTracking()
            .Select(c => new
            {
                c.CylinderId,
                c.SequentialNumber,
                LabelToken = c.LabelToken != null ? c.LabelToken.Value : null,
                State = c.State.ToString(),
                c.OccurrenceNotes,
                c.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var cylinderHistory = await _dbContext.CylinderHistory
            .AsNoTracking()
            .OrderByDescending(h => h.Timestamp)
            .Select(h => new
            {
                h.Id,
                h.CylinderId,
                EventType = h.EventType.ToString(),
                h.Details,
                h.OrderId,
                h.Timestamp
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            customers,
            orders,
            cylinderRefs,
            cylinders,
            cylinderHistory,
            appSettings = await GetAppSettings(cancellationToken),
            omittedTables = Array.Empty<string>()
        });
    }

    [HttpGet("customer/{customerId}")]
    public async Task<IActionResult> GetCustomerSnapshot(Guid customerId, CancellationToken cancellationToken)
    {
        var unavailable = await EnsureDebugEnabled(cancellationToken);
        if (unavailable is not null) return unavailable;

        var customer = await _dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);

        if (customer == null)
        {
            return NotFound(new { error = "Cliente não encontrado" });
        }

        var orders = await _dbContext.Orders
            .AsNoTracking()
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new
            {
                o.OrderId,
                o.CustomerId,
                Status = o.Status.ToString(),
                o.CreatedAt,
                o.CompletedAt,
                o.NotifiedAt
            })
            .ToListAsync(cancellationToken);

        var orderIds = orders.Select(o => o.OrderId).ToList();

        var cylinderRefs = await _dbContext.CylinderRefs
            .AsNoTracking()
            .Where(cr => orderIds.Contains(cr.OrderId))
            .Select(cr => new
            {
                cr.OrderId,
                cr.CylinderId,
                State = cr.State.ToString()
            })
            .ToListAsync(cancellationToken);

        var cylinderIds = cylinderRefs.Select(cr => cr.CylinderId).Distinct().ToList();

        var cylinders = await _dbContext.Cylinders
            .AsNoTracking()
            .Where(c => cylinderIds.Contains(c.CylinderId))
            .Select(c => new
            {
                c.CylinderId,
                c.SequentialNumber,
                LabelToken = c.LabelToken != null ? c.LabelToken.Value : null,
                State = c.State.ToString(),
                c.OccurrenceNotes,
                c.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var cylinderHistory = await _dbContext.CylinderHistory
            .AsNoTracking()
            .Where(h => cylinderIds.Contains(h.CylinderId))
            .OrderByDescending(h => h.Timestamp)
            .Select(h => new
            {
                h.Id,
                h.CylinderId,
                EventType = h.EventType.ToString(),
                h.Details,
                h.OrderId,
                h.Timestamp
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            customer = new
            {
                customer.CustomerId,
                customer.Name,
                Phone = customer.Phone.Value,
                PhoneType = customer.PhoneType.ToString(),
                customer.CreatedAt
            },
            orders,
            cylinderRefs,
            cylinders,
            cylinderHistory,
            omittedTables = new[]
            {
                "AppSettings (configuração global)"
            }
        });
    }

    private async Task<DebugDatabaseExport> BuildDatabaseExport(CancellationToken cancellationToken)
    {
        return new DebugDatabaseExport
        {
            ExportedAt = DateTime.UtcNow,
            Customers = await _dbContext.Customers
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new DebugCustomerRow(c.CustomerId, c.Name, c.Phone.Value, c.PhoneType.ToString(), c.CreatedAt))
                .ToListAsync(cancellationToken),
            Orders = await _dbContext.Orders
                .AsNoTracking()
                .OrderBy(o => o.CreatedAt)
                .Select(o => new DebugOrderRow(o.OrderId, o.CustomerId, o.Status.ToString(), o.CreatedAt, o.CompletedAt, o.NotifiedAt))
                .ToListAsync(cancellationToken),
            CylinderRefs = await _dbContext.CylinderRefs
                .AsNoTracking()
                .OrderBy(cr => cr.OrderId)
                .ThenBy(cr => cr.CylinderId)
                .Select(cr => new DebugCylinderRefRow(cr.OrderId, cr.CylinderId, cr.State.ToString()))
                .ToListAsync(cancellationToken),
            Cylinders = await _dbContext.Cylinders
                .AsNoTracking()
                .OrderBy(c => c.SequentialNumber)
                .Select(c => new DebugCylinderRow(c.CylinderId, c.SequentialNumber, c.LabelToken != null ? c.LabelToken.Value : null, c.State.ToString(), c.OccurrenceNotes, c.CreatedAt))
                .ToListAsync(cancellationToken),
            CylinderHistory = await _dbContext.CylinderHistory
                .AsNoTracking()
                .OrderBy(h => h.Timestamp)
                .Select(h => new DebugCylinderHistoryRow(h.Id, h.CylinderId, h.EventType.ToString(), h.Details, h.OrderId, h.Timestamp))
                .ToListAsync(cancellationToken),
            AppSettings = await GetAppSettings(cancellationToken)
        };
    }

    private Task<List<DebugAppSettingsRow>> GetAppSettings(CancellationToken cancellationToken)
    {
        return _dbContext.AppSettings
            .AsNoTracking()
            .Select(a => new DebugAppSettingsRow(
                a.AppSettingsId,
                a.StoreName,
                a.StorePhone,
                a.StoreLink,
                a.AppTitle,
                a.WhatsAppMessageTemplate,
                a.WelcomeMessageTemplate,
                a.ThankYouMessageTemplate,
                a.PrinterType,
                a.LabelWidthMm,
                a.LabelHeightMm,
                a.DebugEnabled,
                a.SoundNotificationsDisabled,
                a.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    [HttpDelete("customers/{customerId}")]
    public async Task<IActionResult> DeleteCustomer(Guid customerId, CancellationToken cancellationToken)
    {
        var unavailable = await EnsureDebugEnabled(cancellationToken);
        if (unavailable is not null) return unavailable;

        var customer = await _dbContext.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);
        if (customer == null) return NotFound(new { error = "Linha não encontrada" });

        _dbContext.Customers.Remove(customer);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("orders/{orderId}")]
    public async Task<IActionResult> DeleteOrder(Guid orderId, CancellationToken cancellationToken)
    {
        var unavailable = await EnsureDebugEnabled(cancellationToken);
        if (unavailable is not null) return unavailable;

        var order = await _dbContext.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId, cancellationToken);
        if (order == null) return NotFound(new { error = "Linha não encontrada" });

        _dbContext.Orders.Remove(order);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("cylinder-refs/{orderId}/{cylinderId}")]
    public async Task<IActionResult> DeleteCylinderRef(Guid orderId, Guid cylinderId, CancellationToken cancellationToken)
    {
        var unavailable = await EnsureDebugEnabled(cancellationToken);
        if (unavailable is not null) return unavailable;

        var cylinderRef = await _dbContext.CylinderRefs
            .FirstOrDefaultAsync(cr => cr.OrderId == orderId && cr.CylinderId == cylinderId, cancellationToken);
        if (cylinderRef == null) return NotFound(new { error = "Linha não encontrada" });

        _dbContext.CylinderRefs.Remove(cylinderRef);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("cylinders/{cylinderId}")]
    public async Task<IActionResult> DeleteCylinder(Guid cylinderId, CancellationToken cancellationToken)
    {
        var unavailable = await EnsureDebugEnabled(cancellationToken);
        if (unavailable is not null) return unavailable;

        var cylinder = await _dbContext.Cylinders.FirstOrDefaultAsync(c => c.CylinderId == cylinderId, cancellationToken);
        if (cylinder == null) return NotFound(new { error = "Linha não encontrada" });

        _dbContext.Cylinders.Remove(cylinder);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("cylinder-history/{historyId}")]
    public async Task<IActionResult> DeleteCylinderHistory(Guid historyId, CancellationToken cancellationToken)
    {
        var unavailable = await EnsureDebugEnabled(cancellationToken);
        if (unavailable is not null) return unavailable;

        var historyEntry = await _dbContext.CylinderHistory.FirstOrDefaultAsync(h => h.Id == historyId, cancellationToken);
        if (historyEntry == null) return NotFound(new { error = "Linha não encontrada" });

        _dbContext.CylinderHistory.Remove(historyEntry);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<IActionResult?> EnsureDebugEnabled(CancellationToken cancellationToken)
    {
        var settings = await _dbContext.AppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.AppSettingsId == Domain.Entities.AppSettings.SingletonId, cancellationToken);

        if (settings?.DebugEnabled == true)
        {
            return null;
        }

        return NotFound(new { error = "Debug desativado" });
    }
}

public class DebugDatabaseExport
{
    public DateTime ExportedAt { get; set; }
    public List<DebugCustomerRow> Customers { get; set; } = new();
    public List<DebugOrderRow> Orders { get; set; } = new();
    public List<DebugCylinderRefRow> CylinderRefs { get; set; } = new();
    public List<DebugCylinderRow> Cylinders { get; set; } = new();
    public List<DebugCylinderHistoryRow> CylinderHistory { get; set; } = new();
    public List<DebugAppSettingsRow> AppSettings { get; set; } = new();
}

public record DebugCustomerRow(Guid CustomerId, string Name, string Phone, string PhoneType, DateTime CreatedAt);
public record DebugOrderRow(Guid OrderId, Guid CustomerId, string Status, DateTime CreatedAt, DateTime? CompletedAt, DateTime? NotifiedAt);
public record DebugCylinderRefRow(Guid OrderId, Guid CylinderId, string State);
public record DebugCylinderRow(Guid CylinderId, long SequentialNumber, string? LabelToken, string State, string? OccurrenceNotes, DateTime CreatedAt);
public record DebugCylinderHistoryRow(Guid Id, Guid CylinderId, string EventType, string? Details, Guid? OrderId, DateTime Timestamp);
public record DebugAppSettingsRow(Guid AppSettingsId, string StoreName, string StorePhone, string StoreLink, string AppTitle, string WhatsAppMessageTemplate, string WelcomeMessageTemplate, string ThankYouMessageTemplate, string PrinterType, int LabelWidthMm, int LabelHeightMm, bool DebugEnabled, bool SoundNotificationsDisabled, DateTime UpdatedAt);
