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
        // Buscar pedido
        var order = await _orderRepository.FindByIdAsync(command.OrderId, cancellationToken);
        if (order == null)
        {
            return Result<DeliverCylinderResultDto>.Failure("Pedido não encontrado");
        }

        // Verificar se pedido está pronto para recolha
        if (order.Status != RefillOrderStatus.ReadyForPickup)
        {
            return Result<DeliverCylinderResultDto>.Failure($"Pedido não está pronto para recolha. Status atual: {order.Status}");
        }

        // Verificar se botija pertence ao pedido
        var orderCylinderIds = order.Cylinders.Select(c => c.CylinderId).ToList();
        if (!orderCylinderIds.Contains(command.CylinderId))
        {
            return Result<DeliverCylinderResultDto>.Failure("Botija não pertence a este pedido");
        }

        // Buscar botija
        var cylinder = await _cylinderRepository.FindByIdAsync(command.CylinderId, cancellationToken);
        if (cylinder == null)
        {
            return Result<DeliverCylinderResultDto>.Failure("Botija não encontrada");
        }

        // Marcar como entregue
        try
        {
            cylinder.MarkAsDelivered();
        }
        catch (InvalidOperationException ex)
        {
            return Result<DeliverCylinderResultDto>.Failure(ex.Message);
        }

        // Buscar todas as botijas do pedido para verificar se está completo
        var allCylinders = await _cylinderRepository.FindByOrderIdAsync(order.OrderId, cancellationToken);
        var allDelivered = allCylinders.All(c => c.State == CylinderState.Delivered);

        // Atualizar status do pedido baseado nas botijas entregues
        // Precisamos atualizar o estado no CylinderRef também
        order.CheckAndUpdateStatus(allCylinders);
        
        // Se todas entregues, completar o pedido
        if (allDelivered)
        {
            try
            {
                order.Complete();
            }
            catch (InvalidOperationException)
            {
                // Pedido já foi completado ou não está no estado correto
            }
        }

        // Registrar histórico
        var historyEntry = CylinderHistoryEntry.Create(
            cylinder.CylinderId,
            CylinderEventType.Delivered,
            "Botija entregue ao cliente",
            order.OrderId);
        await _historyRepository.AddAsync(historyEntry, cancellationToken);

        await _cylinderRepository.SaveChangesAsync(cancellationToken);
        await _orderRepository.SaveChangesAsync(cancellationToken);
        await _historyRepository.SaveChangesAsync(cancellationToken);

        var totalCylinders = allCylinders.Count;
        var deliveredCylinders = allCylinders.Count(c => c.State == CylinderState.Delivered);

        return Result<DeliverCylinderResultDto>.Success(new DeliverCylinderResultDto
        {
            CylinderId = cylinder.CylinderId,
            State = cylinder.State.ToString(),
            OrderId = order.OrderId,
            OrderStatus = order.Status.ToString(),
            TotalCylinders = totalCylinders,
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
