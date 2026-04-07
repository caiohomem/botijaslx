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
    public async Task<IActionResult> GetStats([FromQuery] int days = 7, CancellationToken cancellationToken = default)
    {
        var safeDays = days is 7 or 30 or 60 ? days : 7;
        var stats = await _statsQuery.GetStatsAsync(safeDays, cancellationToken);
        return Ok(stats);
    }
}
