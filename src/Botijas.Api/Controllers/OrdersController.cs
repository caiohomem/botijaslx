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
    private readonly CloseIntakeCommandHandler _closeIntakeHandler;
    private readonly AddCylinderToOrderCommandHandler _addCylinderHandler;
    private readonly AddCylindersToOrderBatchCommandHandler _addCylindersHandler;
    private readonly ScanCylinderToOrderCommandHandler _scanHandler;
    private readonly GetReadyForPickupQueryHandler _getReadyForPickupHandler;
    private readonly DeliverCylinderCommandHandler _deliverCylinderHandler;
    private readonly MarkOrderNotifiedCommandHandler _markNotifiedHandler;
    private readonly MarkOrderShippedCommandHandler _markShippedHandler;
    private readonly CancelOrderCommandHandler _cancelOrderHandler;
    private readonly DeleteEmptyOpenOrderCommandHandler _deleteEmptyOpenOrderHandler;

    public OrdersController(
        CreateOrderCommandHandler createHandler,
        CloseIntakeCommandHandler closeIntakeHandler,
        AddCylinderToOrderCommandHandler addCylinderHandler,
        AddCylindersToOrderBatchCommandHandler addCylindersHandler,
        ScanCylinderToOrderCommandHandler scanHandler,
        GetReadyForPickupQueryHandler getReadyForPickupHandler,
        DeliverCylinderCommandHandler deliverCylinderHandler,
        MarkOrderNotifiedCommandHandler markNotifiedHandler,
        MarkOrderShippedCommandHandler markShippedHandler,
        CancelOrderCommandHandler cancelOrderHandler,
        DeleteEmptyOpenOrderCommandHandler deleteEmptyOpenOrderHandler)
    {
        _createHandler = createHandler;
        _closeIntakeHandler = closeIntakeHandler;
        _addCylinderHandler = addCylinderHandler;
        _addCylindersHandler = addCylindersHandler;
        _scanHandler = scanHandler;
        _getReadyForPickupHandler = getReadyForPickupHandler;
        _deliverCylinderHandler = deliverCylinderHandler;
        _markNotifiedHandler = markNotifiedHandler;
        _markShippedHandler = markShippedHandler;
        _cancelOrderHandler = cancelOrderHandler;
        _deleteEmptyOpenOrderHandler = deleteEmptyOpenOrderHandler;
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

    /// <summary>
    /// Fecha uma entrada de botijas (existentes + novas) numa única operação.
    /// </summary>
    [HttpPost("intake")]
    public async Task<IActionResult> CloseIntake(
        [FromBody] CloseIntakeCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _closeIntakeHandler.Handle(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
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

    [HttpPost("{orderId}/cylinders/batch")]
    public async Task<IActionResult> AddCylindersBatch(
        Guid orderId,
        [FromBody] AddCylindersToOrderBatchCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _addCylindersHandler.Handle(command with { OrderId = orderId }, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new { cylinders = result.Value });
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

    /// <summary>
    /// Marca pedido de envio como expedido
    /// </summary>
    [HttpPost("{orderId}/mark-shipped")]
    public async Task<IActionResult> MarkShipped(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await _markShippedHandler.Handle(
            new MarkOrderShippedCommand(orderId),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Apaga um pedido Open sem botijas (órfão).
    /// </summary>
    [HttpDelete("{orderId}")]
    public async Task<IActionResult> DeleteEmptyOpenOrder(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await _deleteEmptyOpenOrderHandler.Handle(
            new DeleteEmptyOpenOrderCommand(orderId),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return NoContent();
    }

    /// <summary>
    /// Cancela um pedido com observação
    /// </summary>
    [HttpPost("{orderId}/cancel")]
    public async Task<IActionResult> CancelOrder(
        Guid orderId,
        [FromBody] CancelOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _cancelOrderHandler.Handle(
            new CancelOrderCommand(orderId, request.Notes),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }
}

public record CancelOrderRequest(string Notes);
