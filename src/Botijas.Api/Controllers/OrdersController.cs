using Botijas.Application.Orders.Commands;
using Botijas.Application.Pickup.Commands;
using Botijas.Application.Pickup.Queries;
using Microsoft.AspNetCore.Mvc;

namespace Botijas.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly CreateOrderCommandHandler _createHandler;
    private readonly AddCylinderToOrderCommandHandler _addCylinderHandler;
    private readonly ScanCylinderToOrderCommandHandler _scanHandler;
    private readonly GetReadyForPickupQueryHandler _getReadyForPickupHandler;
    private readonly DeliverCylinderCommandHandler _deliverCylinderHandler;
    private readonly MarkOrderNotifiedCommandHandler _markNotifiedHandler;

    public OrdersController(
        CreateOrderCommandHandler createHandler,
        AddCylinderToOrderCommandHandler addCylinderHandler,
        ScanCylinderToOrderCommandHandler scanHandler,
        GetReadyForPickupQueryHandler getReadyForPickupHandler,
        DeliverCylinderCommandHandler deliverCylinderHandler,
        MarkOrderNotifiedCommandHandler markNotifiedHandler)
    {
        _createHandler = createHandler;
        _addCylinderHandler = addCylinderHandler;
        _scanHandler = scanHandler;
        _getReadyForPickupHandler = getReadyForPickupHandler;
        _deliverCylinderHandler = deliverCylinderHandler;
        _markNotifiedHandler = markNotifiedHandler;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderCommand command, CancellationToken cancellationToken)
    {
        var result = await _createHandler.Handle(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.OrderId }, result.Value);
    }

    [HttpPost("{orderId}/cylinders")]
    public async Task<IActionResult> AddCylinder(
        Guid orderId,
        [FromBody] AddCylinderToOrderCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _addCylinderHandler.Handle(command with { OrderId = orderId }, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    [HttpPost("{orderId}/cylinders/scan")]
    public async Task<IActionResult> ScanCylinder(
        Guid orderId,
        [FromBody] ScanCylinderToOrderCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _scanHandler.Handle(command with { OrderId = orderId }, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        // TODO: Implementar quando necessário
        return NotFound();
    }

    /// <summary>
    /// Lista pedidos prontos para recolha
    /// </summary>
    [HttpGet("ready-for-pickup")]
    public async Task<IActionResult> GetReadyForPickup([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var result = await _getReadyForPickupHandler.Handle(new GetReadyForPickupQuery(search), cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new { orders = result.Value });
    }

    /// <summary>
    /// Entrega uma botija ao cliente
    /// </summary>
    [HttpPost("{orderId}/cylinders/{cylinderId}/deliver")]
    public async Task<IActionResult> DeliverCylinder(
        Guid orderId,
        Guid cylinderId,
        CancellationToken cancellationToken)
    {
        var result = await _deliverCylinderHandler.Handle(
            new DeliverCylinderCommand(orderId, cylinderId), 
            cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Marca pedido como notificado (após enviar WhatsApp)
    /// </summary>
    [HttpPost("{orderId}/mark-notified")]
    public async Task<IActionResult> MarkNotified(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await _markNotifiedHandler.Handle(
            new MarkOrderNotifiedCommand(orderId),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }
}
