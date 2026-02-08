using Botijas.Application.Cylinders.Queries;
using Botijas.Application.Filling.Commands;
using Botijas.Application.Filling.Queries;
using Microsoft.AspNetCore.Mvc;

namespace Botijas.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CylindersController : ControllerBase
{
    private readonly GetFillingQueueQueryHandler _getFillingQueueHandler;
    private readonly MarkCylinderReadyCommandHandler _markReadyHandler;
    private readonly ReportCylinderProblemCommandHandler _reportProblemHandler;
    private readonly AssignLabelCommandHandler _assignLabelHandler;
    private readonly GetCylinderHistoryQueryHandler _getHistoryHandler;
    private readonly GetCylinderByTokenQueryHandler _getByTokenHandler;

    public CylindersController(
        GetFillingQueueQueryHandler getFillingQueueHandler,
        MarkCylinderReadyCommandHandler markReadyHandler,
        ReportCylinderProblemCommandHandler reportProblemHandler,
        AssignLabelCommandHandler assignLabelHandler,
        GetCylinderHistoryQueryHandler getHistoryHandler,
        GetCylinderByTokenQueryHandler getByTokenHandler)
    {
        _getFillingQueueHandler = getFillingQueueHandler;
        _markReadyHandler = markReadyHandler;
        _reportProblemHandler = reportProblemHandler;
        _assignLabelHandler = assignLabelHandler;
        _getHistoryHandler = getHistoryHandler;
        _getByTokenHandler = getByTokenHandler;
    }

    /// <summary>
    /// Lista a fila de enchimento (botijas pendentes)
    /// </summary>
    [HttpGet("filling-queue")]
    public async Task<IActionResult> GetFillingQueue(CancellationToken cancellationToken)
    {
        var result = await _getFillingQueueHandler.Handle(new GetFillingQueueQuery(), cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new { cylinders = result.Value });
    }

    /// <summary>
    /// Marca uma botija como pronta (cheia)
    /// </summary>
    [HttpPost("{cylinderId}/mark-ready")]
    public async Task<IActionResult> MarkReady(Guid cylinderId, CancellationToken cancellationToken)
    {
        var result = await _markReadyHandler.Handle(new MarkCylinderReadyCommand(cylinderId), cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Reporta um problema com uma botija
    /// </summary>
    [HttpPost("{cylinderId}/report-problem")]
    public async Task<IActionResult> ReportProblem(
        Guid cylinderId,
        [FromBody] ReportProblemRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _reportProblemHandler.Handle(
            new ReportCylinderProblemCommand(cylinderId, request.Type, request.Notes),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }
    /// <summary>
    /// Atribui ou substitui etiqueta de uma botija
    /// </summary>
    [HttpPost("{cylinderId}/assign-label")]
    public async Task<IActionResult> AssignLabel(
        Guid cylinderId,
        [FromBody] AssignLabelRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _assignLabelHandler.Handle(
            new AssignLabelCommand(cylinderId, request.QrToken),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }
    /// <summary>
    /// Busca histórico de uma botija
    /// </summary>
    [HttpGet("{cylinderId}/history")]
    public async Task<IActionResult> GetHistory(Guid cylinderId, CancellationToken cancellationToken)
    {
        var result = await _getHistoryHandler.Handle(
            new GetCylinderHistoryQuery(cylinderId),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Busca botija por QR token
    /// </summary>
    [HttpGet("scan/{qrToken}")]
    public async Task<IActionResult> ScanCylinder(string qrToken, CancellationToken cancellationToken)
    {
        var result = await _getByTokenHandler.Handle(
            new GetCylinderByTokenQuery(qrToken),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return NotFound(new { error = result.Error });
        }

        return Ok(result.Value);
    }
}

public record ReportProblemRequest(string Type, string Notes);
public record AssignLabelRequest(string QrToken);
