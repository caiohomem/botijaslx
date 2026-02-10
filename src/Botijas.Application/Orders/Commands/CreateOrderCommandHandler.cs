using Botijas.Application.Common;
using Botijas.Domain.Entities;
using Botijas.Domain.Repositories;

namespace Botijas.Application.Orders.Commands;

public class CreateOrderCommandHandler
{
    private readonly IRefillOrderRepository _orderRepository;
    private readonly ICustomerRepository _customerRepository;

    public CreateOrderCommandHandler(
        IRefillOrderRepository orderRepository,
        ICustomerRepository customerRepository)
    {
        _orderRepository = orderRepository;
        _customerRepository = customerRepository;
    }

    public async Task<Result<OrderDto>> Handle(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        // Verificar se cliente existe
        var customer = await _customerRepository.FindByIdAsync(command.CustomerId, cancellationToken);
        if (customer == null)
        {
            return Result<OrderDto>.Failure("Customer not found");
        }

        // Reuse an existing open order for this customer (idempotent create).
        var openOrders = await _orderRepository.FindOpenOrdersByCustomerAsync(command.CustomerId, cancellationToken);
        var existingOpenOrder = openOrders
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefault();

        if (existingOpenOrder != null)
        {
            return Result<OrderDto>.Success(new OrderDto
            {
                OrderId = existingOpenOrder.OrderId,
                CustomerId = existingOpenOrder.CustomerId,
                Status = existingOpenOrder.Status.ToString(),
                CreatedAt = existingOpenOrder.CreatedAt,
                CompletedAt = existingOpenOrder.CompletedAt,
                CylinderCount = existingOpenOrder.Cylinders.Count
            });
        }

        var order = RefillOrder.Create(command.CustomerId);
        await _orderRepository.AddAsync(order, cancellationToken);
        await _orderRepository.SaveChangesAsync(cancellationToken);

        return Result<OrderDto>.Success(new OrderDto
        {
            OrderId = order.OrderId,
            CustomerId = order.CustomerId,
            Status = order.Status.ToString(),
            CreatedAt = order.CreatedAt,
            CylinderCount = order.Cylinders.Count
        });
    }
}
