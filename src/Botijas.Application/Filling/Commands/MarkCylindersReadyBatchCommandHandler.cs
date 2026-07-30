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

        // Membership via CylinderRef; readiness via Cylinder.State (não CylinderRef.State).
        var cylinderIds = order.Cylinders.Select(c => c.CylinderId).ToList();
        if (cylinderIds.Count == 0)
        {
            return Result<BatchReadyResult>.Failure("No cylinders to mark as ready in this order");
        }

        int markedCount = 0;

        foreach (var cylinderId in cylinderIds)
        {
            var cylinder = await _cylinderRepository.FindByIdAsync(cylinderId, cancellationToken);
            if (cylinder == null)
            {
                continue;
            }

            try
            {
                if (!RefillOrder.IsCylinderReadyForPickup(cylinder.State))
                {
                    cylinder.MarkAsReady();
                    markedCount++;

                    var historyEntry = CylinderHistoryEntry.Create(
                        cylinder.CylinderId,
                        CylinderEventType.MarkedReady,
                        "Botija marcada como cheia (lote)",
                        order.OrderId);
                    await _historyRepository.AddAsync(historyEntry, cancellationToken);
                }
            }
            catch (InvalidOperationException)
            {
                // Skip if cylinder cannot be marked as ready
            }
        }

        if (markedCount == 0)
        {
            var orderCylindersCheck = await _cylinderRepository.FindByOrderIdAsync(order.OrderId, cancellationToken);
            if (orderCylindersCheck.All(c => RefillOrder.IsCylinderReadyForPickup(c.State)))
            {
                return Result<BatchReadyResult>.Failure("No cylinders to mark as ready in this order");
            }
        }

        // Get updated cylinders for order status check
        var orderCylinders = await _cylinderRepository.FindByOrderIdAsync(order.OrderId, cancellationToken);

        // Sincronizar refs e recalcular status do pedido.
        order.RecalculateStatus(orderCylinders);

        // Save changes
        await _cylinderRepository.SaveChangesAsync(cancellationToken);
        await _orderRepository.SaveChangesAsync(cancellationToken);
        await _historyRepository.SaveChangesAsync(cancellationToken);

        // Check if order is now complete (all cylinders ready)
        var isOrderComplete = order.Status.ToString() == "ReadyForPickup";

        var customerPendingCylinders = await _cylinderRepository.CountPendingByCustomerAsync(
            order.CustomerId,
            cancellationToken);

        return Result<BatchReadyResult>.Success(new BatchReadyResult
        {
            OrderId = order.OrderId,
            MarkedCount = markedCount,
            IsOrderComplete = isOrderComplete,
            TotalCylindersInOrder = order.Cylinders.Count,
            CustomerPendingCylinders = customerPendingCylinders
        });
    }
}

public class BatchReadyResult
{
    public Guid OrderId { get; set; }
    public int MarkedCount { get; set; }
    public bool IsOrderComplete { get; set; }
    public int TotalCylindersInOrder { get; set; }
    /// <summary>Botijas do mesmo cliente ainda por encher noutros pedidos abertos.</summary>
    public int CustomerPendingCylinders { get; set; }
}
