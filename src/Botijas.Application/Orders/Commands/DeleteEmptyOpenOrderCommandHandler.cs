using Botijas.Application.Common;
using Botijas.Domain.Entities;
using Botijas.Domain.Repositories;

namespace Botijas.Application.Orders.Commands;

public record DeleteEmptyOpenOrderCommand(Guid OrderId);

public class DeleteEmptyOpenOrderCommandHandler
{
    private readonly IRefillOrderRepository _orderRepository;

    public DeleteEmptyOpenOrderCommandHandler(IRefillOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<Result> Handle(DeleteEmptyOpenOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.FindByIdAsync(command.OrderId, cancellationToken);
        if (order == null)
        {
            return Result.Failure("Pedido não encontrado");
        }

        if (order.Status != RefillOrderStatus.Open)
        {
            return Result.Failure("Só é possível apagar pedidos Open vazios");
        }

        if (order.Cylinders.Count > 0)
        {
            return Result.Failure("Pedido ainda tem botijas; não pode ser apagado");
        }

        await _orderRepository.DeleteAsync(order, cancellationToken);
        await _orderRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
