namespace Botijas.Application.Orders.Commands;

public record ScanCylinderToOrderCommand(Guid OrderId, string QrToken);
