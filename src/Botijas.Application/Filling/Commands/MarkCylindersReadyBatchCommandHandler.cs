using Botijas.Application.Common;
using Botijas.Domain.Entities;
using Botijas.Domain.Repositories;

namespace Botijas.Application.Filling.Commands;

public class MarkCylindersReadyBatchCommandHandler
{
    private readonly IRefillOrderRepository _orderRepository;
    private readonly ICylinderRepository _cylinderRepository;
    private readonly ICylinderHistoryRepository _historyRepository;

    public MarkCylindersReadyBatchCommandHandler(
        IRefillOrderRepository orderRepository,
        ICylinderRepository cylinderRepository,
        ICylinderHistoryRepository historyRepository)
    {
        _orderRepository = orderRepository;
        _cylinderRepository = cylinderRepository;
        _historyRepository = historyRepository;
    }

    public async Task<Result<BatchReadyResult>> Handle(
        MarkCylindersReadyBatchCommand command,
        CancellationToken cancellationToken)
    {
        var order = await _orderRepository.FindByIdAsync(command.OrderId, cancellationToken);
        if (order == null)
        {
            return Result<BatchReadyResult>.Failure("Order not found");
        }

        // Get all cylinders in this order that are not yet ready
        var cylinderRefs = order.Cylinders.Where(c => c.State.ToString() != "Ready").ToList();

        if (cylinderRefs.Count == 0)
        {
            return Result<BatchReadyResult>.Failure("No cylinders to mark as ready in this order");
        }

        int markedCount = 0;

        // Mark each cylinder as ready
        foreach (var cylinderRef in cylinderRefs)
        {
            var cylinder = await _cylinderRepository.FindByIdAsync(cylinderRef.CylinderId, cancellationToken);
            if (cylinder != null)
            {
                try
                {
                    cylinder.MarkAsReady();
                    markedCount++;

                    // Registrar histórico
                    var historyEntry = CylinderHistoryEntry.Create(
                        cylinder.CylinderId,
                        CylinderEventType.MarkedReady,
                        "Botija marcada como cheia (lote)",
                        order.OrderId);
                    await _historyRepository.AddAsync(historyEntry, cancellationToken);
                }
                catch (InvalidOperationException)
                {
                    // Skip if cylinder cannot be marked as ready
                }
            }
        }

        // Get updated cylinders for order status check
        var orderCylinders = await _cylinderRepository.FindByOrderIdAsync(order.OrderId, cancellationToken);

        // Atualizar status do pedido
        order.CheckAndUpdateStatus(orderCylinders);

        // Save changes
        await _cylinderRepository.SaveChangesAsync(cancellationToken);
        await _orderRepository.SaveChangesAsync(cancellationToken);
        await _historyRepository.SaveChangesAsync(cancellationToken);

        // Check if order is now complete (all cylinders ready)
        var isOrderComplete = order.Status.ToString() == "ReadyForPickup";

        return Result<BatchReadyResult>.Success(new BatchReadyResult
        {
            OrderId = order.OrderId,
            MarkedCount = markedCount,
            IsOrderComplete = isOrderComplete,
            TotalCylindersInOrder = order.Cylinders.Count
        });
    }
}

public class BatchReadyResult
{
    public Guid OrderId { get; set; }
    public int MarkedCount { get; set; }
    public bool IsOrderComplete { get; set; }
    public int TotalCylindersInOrder { get; set; }
}
