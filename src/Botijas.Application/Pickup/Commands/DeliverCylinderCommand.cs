namespace Botijas.Application.Pickup.Commands;

public record DeliverCylinderCommand(Guid OrderId, Guid CylinderId);
