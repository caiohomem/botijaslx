namespace Botijas.Application.Reports.Queries;

public interface IDashboardStatsQuery
{
    Task<DashboardStatsDto> GetStatsAsync(int days, CancellationToken cancellationToken);
}

public class DashboardStatsDto
{
    public int OrdersOpen { get; set; }
    /// <summary>Pedidos ReadyForPickup com recolha em loja.</summary>
    public int OrdersReadyForPickup { get; set; }
    /// <summary>Pedidos ReadyForPickup com envio para morada.</summary>
    public int OrdersReadyForShipping { get; set; }
    public int OrdersCompletedToday { get; set; }
    public int OrdersCompletedThisWeek { get; set; }
    public int OrdersAwaitingNotification { get; set; }
    public int OrdersAwaitingNotificationPickup { get; set; }
    public int OrdersAwaitingNotificationShipping { get; set; }
    public int CylindersReceived { get; set; }
    /// <summary>Mantido por compatibilidade; preferir métricas de pedidos prontos por canal.</summary>
    public int CylindersReady { get; set; }
    public int CylindersWithProblem { get; set; }
    public int CylindersFilledToday { get; set; }
    public int CylindersReceivedToday { get; set; }
    public int CylindersFilledThisWeek { get; set; }
    public int TotalCustomers { get; set; }
    public IReadOnlyList<DashboardDailySeriesPointDto> DailySeries { get; set; } = [];
}

public class DashboardDailySeriesPointDto
{
    public string Date { get; set; } = string.Empty;
    public int Received { get; set; }
    public int Ready { get; set; }
    public int Delivered { get; set; }
}
