using Botijas.Application.Business.Queries;
using Botijas.Domain.Entities;
using Botijas.Domain.Services;
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

        var historyRaw = await _context.CylinderHistory
            .AsNoTracking()
            .Where(h =>
                (h.EventType == CylinderEventType.MarkedReady ||
                 h.EventType == CylinderEventType.Delivered ||
                 h.EventType == CylinderEventType.ActionUndone) &&
                h.Timestamp.Date >= prevStart)
            .Select(h => new { h.Id, h.EventType, h.Timestamp, h.OrderId, h.Details })
            .ToListAsync(cancellationToken);

        var undoneIds = CylinderHistoryUndo.ExtractUndoneIds(
            historyRaw.Select(h => (h.EventType, h.Details)));

        var history = historyRaw
            .Where(h =>
                (h.EventType == CylinderEventType.MarkedReady || h.EventType == CylinderEventType.Delivered) &&
                !undoneIds.Contains(h.Id))
            .ToList();

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

        // Ritmo operacional = enchimentos feitos (MarkedReady), alinhado com o KPI "Enchimentos".
        var averageDailyFills = safeDays > 0
            ? Math.Round((decimal)fillsProduced / safeDays, 2)
            : 0m;

        const int forecastDays = 30;
        var forecastFills = Math.Round(averageDailyFills * forecastDays, 1);
        var forecastRevenue = Math.Round(forecastFills * price, 2);
        var forecastGas = Math.Round(forecastFills * gasCost, 2);
        var forecastProfit = forecastRevenue - forecastGas;
        var forecastSource = fillsPerSource > 0
            ? Math.Round(forecastFills / fillsPerSource, 2)
            : 0m;

        // Remaining fills in current source cylinder cycle (based on all-time production, excluding undos)
        var allTimeRaw = await _context.CylinderHistory
            .AsNoTracking()
            .Where(h =>
                h.EventType == CylinderEventType.MarkedReady ||
                h.EventType == CylinderEventType.ActionUndone)
            .Select(h => new { h.Id, h.EventType, h.Details })
            .ToListAsync(cancellationToken);
        var allTimeUndone = CylinderHistoryUndo.ExtractUndoneIds(
            allTimeRaw.Select(h => (h.EventType, h.Details)));
        var totalProducedAllTime = allTimeRaw.Count(h =>
            h.EventType == CylinderEventType.MarkedReady && !allTimeUndone.Contains(h.Id));
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

        var monthly = await BuildMonthlyAnalysisAsync(price, gasCost, today, cancellationToken);

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
            FillsChangePercent = PercentChange(prevFillsProduced, fillsProduced),
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
            WeekdayStats = weekdayStats,
            Monthly = monthly
        };
    }

    private const int MonthlyHistoryMonths = 12;
    private const int MonthlyForecastMonths = 3;

    private async Task<BusinessMonthlyAnalysisDto> BuildMonthlyAnalysisAsync(
        decimal price,
        decimal gasCost,
        DateTime today,
        CancellationToken cancellationToken)
    {
        var currentMonthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var historyStart = currentMonthStart.AddMonths(-(MonthlyHistoryMonths - 1));

        var eventsRaw = await _context.CylinderHistory
            .AsNoTracking()
            .Where(h =>
                (h.EventType == CylinderEventType.MarkedReady ||
                 h.EventType == CylinderEventType.Delivered ||
                 h.EventType == CylinderEventType.ActionUndone) &&
                h.Timestamp >= historyStart)
            .Select(h => new { h.Id, h.EventType, h.Timestamp, h.Details })
            .ToListAsync(cancellationToken);

        var monthlyUndone = CylinderHistoryUndo.ExtractUndoneIds(
            eventsRaw.Select(h => (h.EventType, h.Details)));
        var events = eventsRaw
            .Where(h =>
                (h.EventType == CylinderEventType.MarkedReady || h.EventType == CylinderEventType.Delivered) &&
                !monthlyUndone.Contains(h.Id))
            .ToList();

        var months = Enumerable.Range(0, MonthlyHistoryMonths)
            .Select(offset => historyStart.AddMonths(offset))
            .ToList();

        var points = new List<BusinessMonthlyPointDto>();

        foreach (var monthStart in months)
        {
            var monthEnd = monthStart.AddMonths(1);
            var isCurrent = monthStart == currentMonthStart;

            var delivered = events.Count(e =>
                e.EventType == CylinderEventType.Delivered &&
                e.Timestamp >= monthStart && e.Timestamp < monthEnd);
            var filled = events.Count(e =>
                e.EventType == CylinderEventType.MarkedReady &&
                e.Timestamp >= monthStart && e.Timestamp < monthEnd);

            var revenue = delivered * price;
            var monthGasCost = filled * gasCost;

            var point = new BusinessMonthlyPointDto
            {
                Month = monthStart.ToString("yyyy-MM"),
                Label = FormatMonthLabel(monthStart),
                Filled = filled,
                Delivered = delivered,
                Revenue = revenue,
                GasCost = monthGasCost,
                Profit = revenue - monthGasCost,
                IsPartial = isCurrent
            };

            if (isCurrent)
            {
                var daysElapsed = Math.Max(1, (today - monthStart).Days + 1);
                var daysInMonth = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);
                var factor = (decimal)daysInMonth / daysElapsed;

                var projectedDelivered = Math.Round(delivered * factor, 1);
                var projectedFilled = filled * factor;
                var projectedRevenue = Math.Round(projectedDelivered * price, 2);
                var projectedGas = Math.Round(projectedFilled * gasCost, 2);

                point.ProjectedDelivered = projectedDelivered;
                point.ProjectedRevenue = projectedRevenue;
                point.ProjectedProfit = projectedRevenue - projectedGas;
            }

            points.Add(point);
        }

        for (var i = 1; i < points.Count; i++)
        {
            points[i].GrowthPercent = PercentChange(points[i - 1].Delivered, points[i].Delivered);
        }

        var closed = points.Where(p => !p.IsPartial).ToList();
        var closedWithActivity = closed.SkipWhile(p => p.Delivered == 0).ToList();

        var averageGrowth = closedWithActivity.Count > 1
            ? Math.Round(closedWithActivity.Skip(1).Average(p => p.GrowthPercent), 1)
            : 0m;

        var averageDelivered = closedWithActivity.Count > 0
            ? Math.Round((decimal)closedWithActivity.Average(p => p.Delivered), 1)
            : 0m;
        var averageRevenue = closedWithActivity.Count > 0
            ? Math.Round(closedWithActivity.Average(p => p.Revenue), 2)
            : 0m;
        var averageProfit = closedWithActivity.Count > 0
            ? Math.Round(closedWithActivity.Average(p => p.Profit), 2)
            : 0m;

        var best = closedWithActivity.OrderByDescending(p => p.Profit).FirstOrDefault();

        var slope = LinearTrendSlope(closedWithActivity.Select(p => (decimal)p.Delivered).ToList());
        var forecast = BuildForecast(closedWithActivity, points, slope, price, gasCost, currentMonthStart);

        return new BusinessMonthlyAnalysisDto
        {
            History = points,
            Forecast = forecast,
            AverageMonthlyGrowthPercent = averageGrowth,
            AverageMonthlyDelivered = averageDelivered,
            AverageMonthlyRevenue = averageRevenue,
            AverageMonthlyProfit = averageProfit,
            BestMonth = best?.Label,
            BestMonthProfit = best?.Profit ?? 0m,
            TrendSlopePerMonth = Math.Round(slope, 2),
            TotalRevenue = Math.Round(closedWithActivity.Sum(p => p.Revenue), 2),
            TotalProfit = Math.Round(closedWithActivity.Sum(p => p.Profit), 2),
            ClosedMonths = closedWithActivity.Count
        };
    }

    private static List<BusinessMonthlyPointDto> BuildForecast(
        List<BusinessMonthlyPointDto> closedWithActivity,
        List<BusinessMonthlyPointDto> allPoints,
        decimal slope,
        decimal price,
        decimal gasCost,
        DateTime currentMonthStart)
    {
        var forecast = new List<BusinessMonthlyPointDto>();
        if (closedWithActivity.Count == 0)
        {
            return forecast;
        }

        var values = closedWithActivity.Select(p => (decimal)p.Delivered).ToList();
        var intercept = LinearTrendIntercept(values, slope);

        // Ratio between filled and delivered keeps the gas cost realistic when
        // production and pickup volumes differ.
        var totalDelivered = closedWithActivity.Sum(p => p.Delivered);
        var totalFilled = closedWithActivity.Sum(p => p.Filled);
        var filledRatio = totalDelivered > 0 ? (decimal)totalFilled / totalDelivered : 1m;

        for (var i = 1; i <= MonthlyForecastMonths; i++)
        {
            var monthStart = currentMonthStart.AddMonths(i);
            var x = values.Count - 1 + i + (allPoints.Any(p => p.IsPartial) ? 1 : 0);
            var predicted = Math.Max(0m, intercept + slope * x);

            var predictedRevenue = Math.Round(predicted * price, 2);
            var predictedGas = Math.Round(predicted * filledRatio * gasCost, 2);

            forecast.Add(new BusinessMonthlyPointDto
            {
                Month = monthStart.ToString("yyyy-MM"),
                Label = FormatMonthLabel(monthStart),
                Delivered = (int)Math.Round(predicted),
                Filled = (int)Math.Round(predicted * filledRatio),
                Revenue = predictedRevenue,
                GasCost = predictedGas,
                Profit = predictedRevenue - predictedGas,
                IsForecast = true,
                ProjectedDelivered = Math.Round(predicted, 1),
                ProjectedRevenue = predictedRevenue,
                ProjectedProfit = predictedRevenue - predictedGas
            });
        }

        return forecast;
    }

    /// <summary>Declive da regressão linear simples sobre a série (x = 0..n-1).</summary>
    private static decimal LinearTrendSlope(IReadOnlyList<decimal> values)
    {
        if (values.Count < 2) return 0m;

        var n = values.Count;
        var sumX = 0m;
        var sumY = 0m;
        var sumXy = 0m;
        var sumXx = 0m;

        for (var i = 0; i < n; i++)
        {
            sumX += i;
            sumY += values[i];
            sumXy += i * values[i];
            sumXx += (decimal)i * i;
        }

        var denominator = n * sumXx - sumX * sumX;
        if (denominator == 0) return 0m;

        return (n * sumXy - sumX * sumY) / denominator;
    }

    private static decimal LinearTrendIntercept(IReadOnlyList<decimal> values, decimal slope)
    {
        if (values.Count == 0) return 0m;

        var n = values.Count;
        var sumX = 0m;
        var sumY = 0m;

        for (var i = 0; i < n; i++)
        {
            sumX += i;
            sumY += values[i];
        }

        return (sumY - slope * sumX) / n;
    }

    private static string FormatMonthLabel(DateTime month)
    {
        string[] names = ["jan", "fev", "mar", "abr", "mai", "jun", "jul", "ago", "set", "out", "nov", "dez"];
        return $"{names[month.Month - 1]}/{month:yy}";
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
