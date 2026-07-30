namespace Botijas.Application.Orders.Commands;

/// <summary>
/// Fecha uma entrada de botijas numa única operação: botijas já etiquetadas e
/// botijas novas entram no mesmo pedido.
/// </summary>
public record CloseIntakeCommand(
    Guid CustomerId,
    string FulfillmentMethod = "Pickup",
    bool RefillPaid = false,
    bool ShippingPaid = false,
    IReadOnlyList<Guid>? ExistingCylinderIds = null,
    int NewCylinderCount = 0);
