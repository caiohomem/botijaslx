using Botijas.Application.Reports.Queries;
using Microsoft.AspNetCore.Mvc;

namespace Botijas.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly IDashboardStatsQuery _statsQuery;

    public ReportsController(IDashboardStatsQuery statsQuery)
    {
        _statsQuery = statsQuery;
    }

    /// <summary>
    /// Retorna estatísticas do dashboard
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        var stats = await _statsQuery.GetStatsAsync(cancellationToken);
        return Ok(stats);
    }
}
