namespace Botijas.Application.Reports.Queries;

public interface IDashboardStatsQuery
{
    Task<DashboardStatsDto> GetStatsAsync(CancellationToken cancellationToken);
}

public class DashboardStatsDto
{
    public int OrdersOpen { get; set; }
    public int OrdersReadyForPickup { get; set; }
    public int OrdersCompletedToday { get; set; }
    public int OrdersCompletedThisWeek { get; set; }
    public int OrdersAwaitingNotification { get; set; }
    public int CylindersReceived { get; set; }
    public int CylindersReady { get; set; }
    public int CylindersWithProblem { get; set; }
    public int CylindersFilledToday { get; set; }
    public int CylindersFilledThisWeek { get; set; }
    public int TotalCustomers { get; set; }
}
