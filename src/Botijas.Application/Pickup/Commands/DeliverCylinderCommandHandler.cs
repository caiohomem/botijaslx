using Botijas.Application.Common;
using Botijas.Domain.Entities;
using Botijas.Domain.Repositories;

namespace Botijas.Application.Pickup.Commands;

public class DeliverCylinderCommandHandler
{
    private readonly ICylinderRepository _cylinderRepository;
    private readonly IRefillOrderRepository _orderRepository;
    private readonly ICylinderHistoryRepository _historyRepository;

    public DeliverCylinderCommandHandler(
        ICylinderRepository cylinderRepository,
        IRefillOrderRepository orderRepository,
        ICylinderHistoryRepository historyRepository)
    {
        _cylinderRepository = cylinderRepository;
        _orderRepository = orderRepository;
        _historyRepository = historyRepository;
    }

    public async Task<Result<DeliverCylinderResultDto>> Handle(DeliverCylinderCommand command, CancellationToken cancellationToken)
    {
        Result<DeliverCylinderResultDto>? result = null;
        await _orderRepository.ExecuteLockedAsync(command.OrderId, async ct =>
        {
            result = await DeliverLockedAsync(command, ct);
        }, cancellationToken);

        return result ?? Result<DeliverCylinderResultDto>.Failure("Pedido não encontrado");
    }

    private async Task<Result<DeliverCylinderResultDto>> DeliverLockedAsync(
        DeliverCylinderCommand command,
        CancellationToken cancellationToken)
    {
        var order = await _orderRepository.FindByIdAsync(command.OrderId, cancellationToken);
        if (order == null)
        {
            return Result<DeliverCylinderResultDto>.Failure("Pedido não encontrado");
        }

        if (order.FulfillmentMethod != FulfillmentMethod.Pickup)
        {
            return Result<DeliverCylinderResultDto>.Failure("Este pedido está marcado para envio. Use a ação de expedição.");
        }

        var orderCylinderIds = order.Cylinders.Select(c => c.CylinderId).ToList();
        if (!orderCylinderIds.Contains(command.CylinderId))
        {
            return Result<DeliverCylinderResultDto>.Failure("Botija não pertence a este pedido");
        }

        var allCylinders = await _cylinderRepository.FindByOrderIdAsync(order.OrderId, cancellationToken);
        // Pedidos que ficaram Open depois de uma marcação em paralelo estão na Out
        // (todas as botijas já estão prontas) mas o estado ainda não foi promovido.
        if (order.Status == RefillOrderStatus.Open)
        {
            order.RecalculateStatus(allCylinders);
        }

        if (order.Status != RefillOrderStatus.ReadyForPickup)
        {
            return Result<DeliverCylinderResultDto>.Failure($"Pedido não está pronto para recolha. Status atual: {order.Status}");
        }

        var cylinder = allCylinders.FirstOrDefault(c => c.CylinderId == command.CylinderId);
        if (cylinder == null)
        {
            return Result<DeliverCylinderResultDto>.Failure("Botija não encontrada");
        }

        try
        {
            cylinder.MarkAsDelivered();
        }
        catch (InvalidOperationException ex)
        {
            return Result<DeliverCylinderResultDto>.Failure(ex.Message);
        }

        order.RecalculateStatus(allCylinders);

        await _historyRepository.AddAsync(
            CylinderHistoryEntry.Create(
                cylinder.CylinderId,
                CylinderEventType.Delivered,
                "Botija entregue ao cliente",
                order.OrderId),
            cancellationToken);

        await _cylinderRepository.SaveChangesAsync(cancellationToken);
        await _orderRepository.SaveChangesAsync(cancellationToken);
        await _historyRepository.SaveChangesAsync(cancellationToken);

        var deliveredCylinders = allCylinders.Count(c => c.State == CylinderState.Delivered);

        return Result<DeliverCylinderResultDto>.Success(new DeliverCylinderResultDto
        {
            CylinderId = cylinder.CylinderId,
            State = cylinder.State.ToString(),
            OrderId = order.OrderId,
            OrderStatus = order.Status.ToString(),
            TotalCylinders = allCylinders.Count,
            DeliveredCylinders = deliveredCylinders,
            IsOrderComplete = order.Status == RefillOrderStatus.Completed
        });
    }
}

public class DeliverCylinderResultDto
{
    public Guid CylinderId { get; set; }
    public string State { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public int TotalCylinders { get; set; }
    public int DeliveredCylinders { get; set; }
    public bool IsOrderComplete { get; set; }
}
