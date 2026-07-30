using Botijas.Application.Business.Queries;
using Botijas.Domain.Entities;
using Botijas.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Botijas.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BusinessController : ControllerBase
{
    private readonly IBusinessOverviewQuery _overviewQuery;
    private readonly BotijasDbContext _dbContext;

    public BusinessController(IBusinessOverviewQuery overviewQuery, BotijasDbContext dbContext)
    {
        _overviewQuery = overviewQuery;
        _dbContext = dbContext;
    }

    /// <summary>
    /// Overview financeiro do negócio (receita, lucro, forecast, top clientes).
    /// </summary>
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        var overview = await _overviewQuery.GetOverviewAsync(days, cancellationToken);
        return Ok(overview);
    }

    [HttpGet("settings")]
    public async Task<ActionResult<BusinessFinanceSettingsResponse>> GetSettings(CancellationToken cancellationToken)
    {
        var settings = await _dbContext.AppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.AppSettingsId == AppSettings.SingletonId, cancellationToken)
            ?? AppSettings.CreateDefault();

        return Ok(MapFinance(settings));
    }

    [HttpPut("settings")]
    public async Task<ActionResult<BusinessFinanceSettingsResponse>> UpdateSettings(
        [FromBody] UpdateBusinessFinanceRequest request,
        CancellationToken cancellationToken)
    {
        var settings = await _dbContext.AppSettings
            .FirstOrDefaultAsync(s => s.AppSettingsId == AppSettings.SingletonId, cancellationToken);

        if (settings is null)
        {
            settings = AppSettings.CreateDefault();
            await _dbContext.AppSettings.AddAsync(settings, cancellationToken);
        }

        settings.UpdateBusinessFinance(
            request.RefillPriceEur,
            request.SourceCylinderCostEur,
            request.SourceCylinderGasKg,
            request.ConsumerCylinderGasG);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapFinance(settings));
    }

    private static BusinessFinanceSettingsResponse MapFinance(AppSettings settings)
    {
        return new BusinessFinanceSettingsResponse(
            settings.RefillPriceEur,
            settings.SourceCylinderCostEur,
            settings.SourceCylinderGasKg,
            settings.ConsumerCylinderGasG,
            settings.FillsPerSourceCylinder,
            settings.GasCostPerFillEur);
    }
}

public record UpdateBusinessFinanceRequest(
    decimal RefillPriceEur,
    decimal SourceCylinderCostEur,
    decimal SourceCylinderGasKg,
    decimal ConsumerCylinderGasG);

public record BusinessFinanceSettingsResponse(
    decimal RefillPriceEur,
    decimal SourceCylinderCostEur,
    decimal SourceCylinderGasKg,
    decimal ConsumerCylinderGasG,
    decimal FillsPerSourceCylinder,
    decimal GasCostPerFillEur);
