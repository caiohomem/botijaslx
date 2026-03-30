using Botijas.Application.Common;
using Botijas.Domain.Entities;
using Botijas.Domain.Repositories;

namespace Botijas.Application.Pickup.Commands;

public class MarkOrderShippedCommandHandler
{
    private readonly ICylinderRepository _cylinderRepository;
    private readonly IRefillOrderRepository _orderRepository;
    private readonly ICylinderHistoryRepository _historyRepository;

    public MarkOrderShippedCommandHandler(
        ICylinderRepository cylinderRepository,
        IRefillOrderRepository orderRepository,
        ICylinderHistoryRepository historyRepository)
    {
        _cylinderRepository = cylinderRepository;
        _orderRepository = orderRepository;
        _historyRepository = historyRepository;
    }

    public async Task<Result<MarkOrderShippedResultDto>> Handle(
        MarkOrderShippedCommand command,
        CancellationToken cancellationToken)
    {
        var order = await _orderRepository.FindByIdAsync(command.OrderId, cancellationToken);
        if (order == null)
        {
            return Result<MarkOrderShippedResultDto>.Failure("Pedido não encontrado");
        }

        if (order.FulfillmentMethod != FulfillmentMethod.Shipping)
        {
            return Result<MarkOrderShippedResultDto>.Failure("Este pedido não está marcado para envio");
        }

        if (order.Status != RefillOrderStatus.ReadyForPickup)
        {
            return Result<MarkOrderShippedResultDto>.Failure($"Pedido não está pronto para envio. Status atual: {order.Status}");
        }

        var cylinders = await _cylinderRepository.FindByOrderIdAsync(order.OrderId, cancellationToken);
        if (cylinders.Count == 0)
        {
            return Result<MarkOrderShippedResultDto>.Failure("Pedido sem botijas");
        }

        foreach (var cylinder in cylinders.Where(c => c.State != CylinderState.Delivered))
        {
            try
            {
                cylinder.MarkAsDelivered();
            }
            catch (InvalidOperationException ex)
            {
                return Result<MarkOrderShippedResultDto>.Failure(ex.Message);
            }

            var historyEntry = CylinderHistoryEntry.Create(
                cylinder.CylinderId,
                CylinderEventType.Delivered,
                "Botija expedida para envio ao cliente",
                order.OrderId);
            await _historyRepository.AddAsync(historyEntry, cancellationToken);
        }

        order.RecalculateStatus(cylinders);

        try
        {
            order.MarkAsShipped();
        }
        catch (InvalidOperationException ex)
        {
            return Result<MarkOrderShippedResultDto>.Failure(ex.Message);
        }

        await _cylinderRepository.SaveChangesAsync(cancellationToken);
        await _orderRepository.SaveChangesAsync(cancellationToken);
        await _historyRepository.SaveChangesAsync(cancellationToken);

        return Result<MarkOrderShippedResultDto>.Success(new MarkOrderShippedResultDto
        {
            OrderId = order.OrderId,
            OrderStatus = order.Status.ToString(),
            ShippedAt = order.ShippedAt!.Value,
            TotalCylinders = cylinders.Count
        });
    }
}

public class MarkOrderShippedResultDto
{
    public Guid OrderId { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public DateTime ShippedAt { get; set; }
    public int TotalCylinders { get; set; }
}
