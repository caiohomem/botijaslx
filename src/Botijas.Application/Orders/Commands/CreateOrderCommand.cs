namespace Botijas.Application.Orders.Commands;

public record CreateOrderCommand(
    Guid CustomerId,
    string FulfillmentMethod = "Pickup",
    bool RefillPaid = false,
    bool ShippingPaid = false);
