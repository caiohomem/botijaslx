using Botijas.Application.Common;
using Botijas.Domain.Entities;
using Botijas.Domain.Repositories;

namespace Botijas.Application.Orders.Commands;

public class CancelOrderCommandHandler
{
    private readonly IRefillOrderRepository _orderRepository;
    private readonly ICylinderRepository _cylinderRepository;
    private readonly ICylinderHistoryRepository _historyRepository;

    public CancelOrderCommandHandler(
        IRefillOrderRepository orderRepository,
        ICylinderRepository cylinderRepository,
        ICylinderHistoryRepository historyRepository)
    {
        _orderRepository = orderRepository;
        _cylinderRepository = cylinderRepository;
        _historyRepository = historyRepository;
    }

    public async Task<Result<OrderDto>> Handle(CancelOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.FindByIdAsync(command.OrderId, cancellationToken);
        if (order == null)
        {
            return Result<OrderDto>.Failure("Pedido não encontrado");
        }

        try
        {
            order.Cancel(command.Notes);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return Result<OrderDto>.Failure(ex.Message);
        }

        var cylinders = await _cylinderRepository.FindByOrderIdAsync(order.OrderId, cancellationToken);
        foreach (var cylinder in cylinders)
        {
            cylinder.MarkAsReturnedAfterCancellation();
            await _historyRepository.AddAsync(
                CylinderHistoryEntry.Create(
                    cylinder.CylinderId,
                    CylinderEventType.OrderCancelled,
                    $"Pedido cancelado: {order.CancellationNotes}",
                    order.OrderId),
                cancellationToken);
        }

        await _orderRepository.SaveChangesAsync(cancellationToken);
        await _cylinderRepository.SaveChangesAsync(cancellationToken);
        await _historyRepository.SaveChangesAsync(cancellationToken);

        return Result<OrderDto>.Success(new OrderDto
        {
            OrderId = order.OrderId,
            CustomerId = order.CustomerId,
            Status = order.Status.ToString(),
            FulfillmentMethod = order.FulfillmentMethod.ToString(),
            RefillPaid = order.RefillPaid,
            ShippingPaid = order.ShippingPaid,
            CreatedAt = order.CreatedAt,
            CompletedAt = order.CompletedAt,
            ShippedAt = order.ShippedAt,
            CancelledAt = order.CancelledAt,
            CancellationNotes = order.CancellationNotes,
            CylinderCount = order.Cylinders.Count
        });
    }
}
