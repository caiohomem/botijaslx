using Botijas.Application.Common;
using Botijas.Domain.Repositories;

namespace Botijas.Application.Filling.Commands;

public class MarkCylindersReadyBatchCommandHandler
{
    private readonly IRefillOrderRepository _orderRepository;
    private readonly ICylinderRepository _cylinderRepository;

    public MarkCylindersReadyBatchCommandHandler(
        IRefillOrderRepository orderRepository,
        ICylinderRepository cylinderRepository)
    {
        _orderRepository = orderRepository;
        _cylinderRepository = cylinderRepository;
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
                cylinder.MarkReady();
                markedCount++;
            }
        }

        // Save changes
        await _cylinderRepository.SaveChangesAsync(cancellationToken);
        await _orderRepository.SaveChangesAsync(cancellationToken);

        // Check if order is now complete (all cylinders ready)
        var isOrderComplete = order.Cylinders.All(c => c.State.ToString() == "Ready");

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
