namespace Botijas.Application.Orders.Commands;

public record AddCylindersToOrderBatchCommand(Guid OrderId, int Quantity);
