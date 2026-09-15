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
        if (!Enum.TryParse<FulfillmentMethod>(command.FulfillmentMethod, true, out var fulfillmentMethod))
        {
            return Result<OrderDto>.Failure("Fulfillment method inválido");
        }

        // Verificar se cliente existe
        var customer = await _customerRepository.FindByIdAsync(command.CustomerId, cancellationToken);
        if (customer == null)
        {
            return Result<OrderDto>.Failure("Customer not found");
        }

        // Uma visita = um pedido aberto por cliente/modo (mesma regra do CloseIntake).
        var openOrders = await _orderRepository.FindOpenOrdersByCustomerAsync(command.CustomerId, cancellationToken);
        var order = openOrders
            .Where(o => o.FulfillmentMethod == fulfillmentMethod)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefault();

        if (order == null)
        {
            order = RefillOrder.Create(
                command.CustomerId,
                fulfillmentMethod,
                command.RefillPaid,
                command.ShippingPaid);
            await _orderRepository.AddAsync(order, cancellationToken);
        }
        else
        {
            order.UpdateFulfillmentDetails(
                fulfillmentMethod,
                order.RefillPaid || command.RefillPaid,
                order.ShippingPaid || command.ShippingPaid);
        }

        await _orderRepository.SaveChangesAsync(cancellationToken);

        return Result<OrderDto>.Success(new OrderDto
        {
            OrderId = order.OrderId,
            CustomerId = order.CustomerId,
            Status = order.Status.ToString(),
            FulfillmentMethod = order.FulfillmentMethod.ToString(),
            RefillPaid = order.RefillPaid,
            ShippingPaid = order.ShippingPaid,
            CreatedAt = order.CreatedAt,
            ShippedAt = order.ShippedAt,
            CylinderCount = order.Cylinders.Count
        });
    }
}
