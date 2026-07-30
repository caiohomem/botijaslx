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

public class BusinessMonthlyPointDto
{
    /// <summary>Mês no formato yyyy-MM.</summary>
    public string Month { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Filled { get; set; }
    public int Delivered { get; set; }
    public decimal Revenue { get; set; }
    public decimal GasCost { get; set; }
    public decimal Profit { get; set; }
    /// <summary>Variação percentual de entregas face ao mês anterior.</summary>
    public decimal GrowthPercent { get; set; }
    /// <summary>True quando o mês ainda está a decorrer (dados parciais).</summary>
    public bool IsPartial { get; set; }
    /// <summary>Projeção de entregas ao fecho do mês (só para o mês corrente).</summary>
    public decimal? ProjectedDelivered { get; set; }
    public decimal? ProjectedRevenue { get; set; }
    public decimal? ProjectedProfit { get; set; }
    /// <summary>Valor previsto pela tendência (regressão linear) para meses futuros.</summary>
    public bool IsForecast { get; set; }
}

public class BusinessMonthlyAnalysisDto
{
    /// <summary>Histórico mensal fechado + mês corrente (parcial).</summary>
    public IReadOnlyList<BusinessMonthlyPointDto> History { get; set; } = [];
    /// <summary>Meses projetados pela tendência.</summary>
    public IReadOnlyList<BusinessMonthlyPointDto> Forecast { get; set; } = [];
    /// <summary>Crescimento médio mensal (%) nos meses fechados.</summary>
    public decimal AverageMonthlyGrowthPercent { get; set; }
    /// <summary>Média de entregas por mês nos meses fechados.</summary>
    public decimal AverageMonthlyDelivered { get; set; }
    public decimal AverageMonthlyRevenue { get; set; }
    public decimal AverageMonthlyProfit { get; set; }
    /// <summary>Melhor mês fechado por lucro.</summary>
    public string? BestMonth { get; set; }
    public decimal BestMonthProfit { get; set; }
    /// <summary>Inclinação da tendência (entregas por mês).</summary>
    public decimal TrendSlopePerMonth { get; set; }
    /// <summary>Total acumulado nos meses fechados.</summary>
    public decimal TotalRevenue { get; set; }
    public decimal TotalProfit { get; set; }
    public int ClosedMonths { get; set; }
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
    public BusinessMonthlyAnalysisDto Monthly { get; set; } = new();
}
