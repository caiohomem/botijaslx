namespace Botijas.Application.Business.Queries;

public interface IBusinessOverviewQuery
{
    Task<BusinessOverviewDto> GetOverviewAsync(int days, CancellationToken cancellationToken);
}

public class BusinessFinanceSettingsDto
{
    public decimal RefillPriceEur { get; set; }
    public decimal SourceCylinderCostEur { get; set; }
    public decimal SourceCylinderGasKg { get; set; }
    public decimal ConsumerCylinderGasG { get; set; }
    public decimal FillsPerSourceCylinder { get; set; }
    public decimal GasCostPerFillEur { get; set; }
}

public class BusinessDailyPointDto
{
    public string Date { get; set; } = string.Empty;
    public int Filled { get; set; }
    public int Delivered { get; set; }
    public decimal Revenue { get; set; }
    public decimal GasCost { get; set; }
    public decimal Profit { get; set; }
}

public class BusinessTopCustomerDto
{
    public Guid CustomerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DeliveredFills { get; set; }
    public decimal Revenue { get; set; }
    public DateTime? LastDeliveredAt { get; set; }
}

public class BusinessWeekdayStatDto
{
    public int DayOfWeek { get; set; }
    public string DayName { get; set; } = string.Empty;
    public decimal AverageFills { get; set; }
}

public class BusinessOverviewDto
{
    public int Days { get; set; }
    public BusinessFinanceSettingsDto Settings { get; set; } = new();

    public int FillsDelivered { get; set; }
    public int FillsProduced { get; set; }
    public decimal Revenue { get; set; }
    public decimal GasCost { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal MarginPercent { get; set; }
    public decimal SourceCylindersConsumed { get; set; }

    public int PrevFillsDelivered { get; set; }
    public int PrevFillsProduced { get; set; }
    public decimal PrevRevenue { get; set; }
    public decimal PrevGrossProfit { get; set; }
    public decimal RevenueChangePercent { get; set; }
    public decimal ProfitChangePercent { get; set; }
    public decimal FillsChangePercent { get; set; }

    public IReadOnlyList<BusinessDailyPointDto> DailySeries { get; set; } = [];

    public decimal AverageDailyFills { get; set; }
    public int ForecastDays { get; set; }
    public decimal ForecastFills { get; set; }
    public decimal ForecastRevenue { get; set; }
    public decimal ForecastGasCost { get; set; }
    public decimal ForecastProfit { get; set; }
    public decimal ForecastSourceCylinders { get; set; }
    public int DaysUntilNextSourceCylinder { get; set; }

    public int PipelineReadyCount { get; set; }
    public decimal PipelineValue { get; set; }
    public int UnpaidCompletedOrders { get; set; }
    public int ProblemCylinders { get; set; }

    public IReadOnlyList<BusinessTopCustomerDto> TopCustomers { get; set; } = [];
    public IReadOnlyList<BusinessWeekdayStatDto> WeekdayStats { get; set; } = [];
}
