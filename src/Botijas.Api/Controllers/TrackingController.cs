using Botijas.Application.Tracking.Queries;
using Microsoft.AspNetCore.Mvc;

namespace Botijas.Api.Controllers;

/// <summary>
/// Endpoint público de autoatendimento para o cliente final acompanhar o estado das
/// suas próprias botijas/pedidos sem precisar de telefonar ou visitar a loja.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TrackingController : ControllerBase
{
    private readonly GetOrderStatusByPhoneQueryHandler _getStatusHandler;

    public TrackingController(GetOrderStatusByPhoneQueryHandler getStatusHandler)
    {
        _getStatusHandler = getStatusHandler;
    }

    /// <summary>
    /// Consulta o estado dos pedidos de um cliente a partir do telefone.
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus([FromQuery] string phone, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return BadRequest(new { error = "Phone is required" });
        }

        var result = await _getStatusHandler.Handle(new GetOrderStatusByPhoneQuery(phone), cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }
}
