namespace Botijas.Application.Orders.Commands;

public record CancelOrderCommand(Guid OrderId, string Notes);
