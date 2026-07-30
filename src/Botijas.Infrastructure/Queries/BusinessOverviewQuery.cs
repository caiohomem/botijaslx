using Botijas.Application.Business.Queries;
using Botijas.Domain.Entities;
using Botijas.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Botijas.Infrastructure.Queries;

public class BusinessOverviewQuery : IBusinessOverviewQuery
{
    private static readonly string[] WeekdayNamesPt =
        ["Domingo", "Segunda", "Terça", "Quarta", "Quinta", "Sexta", "Sábado"];

    private readonly BotijasDbContext _context;

    public BusinessOverviewQuery(BotijasDbContext context)
    {
        _context = context;
    }

    public async Task<BusinessOverviewDto> GetOverviewAsync(int days, CancellationToken cancellationToken)
    {
        var safeDays = days is >= 1 and <= 365 ? days : 30;
        var today = DateTime.UtcNow.Date;
        var periodStart = today.AddDays(-(safeDays - 1));
        var prevStart = periodStart.AddDays(-safeDays);
        var prevEnd = periodStart.AddDays(-1);

        var settingsEntity = await _context.AppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.AppSettingsId == AppSettings.SingletonId, cancellationToken)
            ?? AppSettings.CreateDefault();

        var settings = new BusinessFinanceSettingsDto
        {
            RefillPriceEur = settingsEntity.RefillPriceEur,
            SourceCylinderCostEur = settingsEntity.SourceCylinderCostEur,
            SourceCylinderGasKg = settingsEntity.SourceCylinderGasKg,
            ConsumerCylinderGasG = settingsEntity.ConsumerCylinderGasG,
            FillsPerSourceCylinder = settingsEntity.FillsPerSourceCylinder,
            GasCostPerFillEur = settingsEntity.GasCostPerFillEur
        };

        var price = settings.RefillPriceEur;
        var gasCost = settings.GasCostPerFillEur;
        var fillsPerSource = settings.FillsPerSourceCylinder;

        var history = await _context.CylinderHistory
            .AsNoTracking()
            .Where(h =>
                (h.EventType == CylinderEventType.MarkedReady || h.EventType == CylinderEventType.Delivered) &&
                h.Timestamp.Date >= prevStart)
            .Select(h => new { h.EventType, h.Timestamp, h.OrderId })
            .ToListAsync(cancellationToken);

        var current = history.Where(h => h.Timestamp.Date >= periodStart).ToList();
        var previous = history.Where(h => h.Timestamp.Date >= prevStart && h.Timestamp.Date <= prevEnd).ToList();

        var fillsDelivered = current.Count(h => h.EventType == CylinderEventType.Delivered);
        var fillsProduced = current.Count(h => h.EventType == CylinderEventType.MarkedReady);
        var prevFillsDelivered = previous.Count(h => h.EventType == CylinderEventType.Delivered);
        var prevFillsProduced = previous.Count(h => h.EventType == CylinderEventType.MarkedReady);

        var revenue = fillsDelivered * price;
        var periodGasCost = fillsProduced * gasCost;
        var grossProfit = revenue - periodGasCost;
        var marginPercent = revenue > 0 ? Math.Round(grossProfit / revenue * 100m, 1) : 0m;
        var sourceConsumed = fillsPerSource > 0
            ? Math.Round(fillsProduced / fillsPerSource, 2)
            : 0m;

        var prevRevenue = prevFillsDelivered * price;
        var prevGasCost = prevFillsProduced * gasCost;
        var prevProfit = prevRevenue - prevGasCost;

        var dailySeries = Enumerable.Range(0, safeDays)
            .Select(offset => periodStart.AddDays(offset))
            .Select(day =>
            {
                var filled = current.Count(h => h.EventType == CylinderEventType.MarkedReady && h.Timestamp.Date == day);
                var delivered = current.Count(h => h.EventType == CylinderEventType.Delivered && h.Timestamp.Date == day);
                var dayRevenue = delivered * price;
                var dayGas = filled * gasCost;
                return new BusinessDailyPointDto
                {
                    Date = day.ToString("yyyy-MM-dd"),
                    Filled = filled,
                    Delivered = delivered,
                    Revenue = dayRevenue,
                    GasCost = dayGas,
                    Profit = dayRevenue - dayGas
                };
            })
            .ToList();

        var averageDailyFills = safeDays > 0
            ? Math.Round((decimal)fillsDelivered / safeDays, 2)
            : 0m;

        const int forecastDays = 30;
        var forecastFills = Math.Round(averageDailyFills * forecastDays, 1);
        var forecastRevenue = Math.Round(forecastFills * price, 2);
        var forecastGas = Math.Round(forecastFills * gasCost, 2);
        var forecastProfit = forecastRevenue - forecastGas;
        var forecastSource = fillsPerSource > 0
            ? Math.Round(forecastFills / fillsPerSource, 2)
            : 0m;

        // Remaining fills in current source cylinder cycle (based on all-time production)
        var totalProducedAllTime = await _context.CylinderHistory
            .CountAsync(h => h.EventType == CylinderEventType.MarkedReady, cancellationToken);
        var fillsPerSourceInt = (int)Math.Max(1, Math.Floor(fillsPerSource));
        var fillsIntoCurrentSource = totalProducedAllTime % fillsPerSourceInt;
        var fillsLeftInSource = Math.Max(0, fillsPerSourceInt - fillsIntoCurrentSource);
        var daysUntilNext = averageDailyFills > 0
            ? (int)Math.Ceiling(fillsLeftInSource / averageDailyFills)
            : 0;

        var pipelineReadyCount = await _context.Cylinders
            .CountAsync(c => c.State == CylinderState.Ready, cancellationToken);

        var problemCylinders = await _context.Cylinders
            .CountAsync(c => c.State == CylinderState.Problem, cancellationToken);

        var unpaidCompletedOrders = await _context.Orders
            .CountAsync(o =>
                o.Status == RefillOrderStatus.Completed &&
                !o.RefillPaid &&
                o.CompletedAt != null &&
                o.CompletedAt.Value.Date >= periodStart,
                cancellationToken);

        // Top customers by delivered fills in period
        var deliveredWithOrder = current
            .Where(h => h.EventType == CylinderEventType.Delivered && h.OrderId != null)
            .GroupBy(h => h.OrderId!.Value)
            .Select(g => new { OrderId = g.Key, Count = g.Count(), LastAt = g.Max(x => x.Timestamp) })
            .ToList();

        var orderIds = deliveredWithOrder.Select(x => x.OrderId).ToList();
        var orders = await _context.Orders
            .AsNoTracking()
            .Where(o => orderIds.Contains(o.OrderId))
            .Select(o => new { o.OrderId, o.CustomerId })
            .ToListAsync(cancellationToken);

        var customerIds = orders.Select(o => o.CustomerId).Distinct().ToList();
        var customers = await _context.Customers
            .AsNoTracking()
            .Where(c => customerIds.Contains(c.CustomerId))
            .Select(c => new { c.CustomerId, c.Name })
            .ToListAsync(cancellationToken);

        var topCustomers = orders
            .Join(deliveredWithOrder, o => o.OrderId, d => d.OrderId, (o, d) => new { o.CustomerId, d.Count, d.LastAt })
            .GroupBy(x => x.CustomerId)
            .Select(g =>
            {
                var customer = customers.FirstOrDefault(c => c.CustomerId == g.Key);
                return new BusinessTopCustomerDto
                {
                    CustomerId = g.Key,
                    Name = customer?.Name ?? "Cliente",
                    DeliveredFills = g.Sum(x => x.Count),
                    Revenue = g.Sum(x => x.Count) * price,
                    LastDeliveredAt = g.Max(x => x.LastAt)
                };
            })
            .OrderByDescending(c => c.DeliveredFills)
            .Take(10)
            .ToList();

        // Weekday seasonality from delivered fills in period
        var weekdayStats = Enumerable.Range(0, 7)
            .Select(dow =>
            {
                var daysOfType = dailySeries.Count(p => DateTime.Parse(p.Date).DayOfWeek == (DayOfWeek)dow);
                var total = dailySeries
                    .Where(p => DateTime.Parse(p.Date).DayOfWeek == (DayOfWeek)dow)
                    .Sum(p => p.Delivered);
                return new BusinessWeekdayStatDto
                {
                    DayOfWeek = dow,
                    DayName = WeekdayNamesPt[dow],
                    AverageFills = daysOfType > 0 ? Math.Round((decimal)total / daysOfType, 2) : 0m
                };
            })
            .ToList();

        return new BusinessOverviewDto
        {
            Days = safeDays,
            Settings = settings,
            FillsDelivered = fillsDelivered,
            FillsProduced = fillsProduced,
            Revenue = revenue,
            GasCost = periodGasCost,
            GrossProfit = grossProfit,
            MarginPercent = marginPercent,
            SourceCylindersConsumed = sourceConsumed,
            PrevFillsDelivered = prevFillsDelivered,
            PrevFillsProduced = prevFillsProduced,
            PrevRevenue = prevRevenue,
            PrevGrossProfit = prevProfit,
            RevenueChangePercent = PercentChange(prevRevenue, revenue),
            ProfitChangePercent = PercentChange(prevProfit, grossProfit),
            FillsChangePercent = PercentChange(prevFillsDelivered, fillsDelivered),
            DailySeries = dailySeries,
            AverageDailyFills = averageDailyFills,
            ForecastDays = forecastDays,
            ForecastFills = forecastFills,
            ForecastRevenue = forecastRevenue,
            ForecastGasCost = forecastGas,
            ForecastProfit = forecastProfit,
            ForecastSourceCylinders = forecastSource,
            DaysUntilNextSourceCylinder = daysUntilNext,
            PipelineReadyCount = pipelineReadyCount,
            PipelineValue = pipelineReadyCount * price,
            UnpaidCompletedOrders = unpaidCompletedOrders,
            ProblemCylinders = problemCylinders,
            TopCustomers = topCustomers,
            WeekdayStats = weekdayStats
        };
    }

    private static decimal PercentChange(decimal previous, decimal current)
    {
        if (previous == 0)
        {
            return current == 0 ? 0 : 100;
        }

        return Math.Round((current - previous) / Math.Abs(previous) * 100m, 1);
    }
}
