namespace Botijas.Application.Orders.Commands;

public record AddCylinderToOrderCommand(Guid OrderId, Guid? CylinderId);
