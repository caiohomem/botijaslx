using Botijas.Application.Common;
using Botijas.Domain.Repositories;
using Botijas.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Botijas.Api.Handlers;

public class DeleteCustomerHandler
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IRefillOrderRepository _orderRepository;
    private readonly BotijasDbContext _context;

    public DeleteCustomerHandler(
        ICustomerRepository customerRepository,
        IRefillOrderRepository orderRepository,
        BotijasDbContext context)
    {
        _customerRepository = customerRepository;
        _orderRepository = orderRepository;
        _context = context;
    }

    public async Task<Result> Handle(Guid customerId, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.FindByIdAsync(customerId, cancellationToken);
        if (customer == null)
            return Result.Failure("Cliente não encontrado");

        // Check if customer has any orders - if so, cannot delete
        var orders = await _orderRepository.FindAllByCustomerAsync(customerId, cancellationToken);
        if (orders.Any())
            return Result.Failure("Não é possível eliminar clientes com histórico de pedidos");

        // If no orders, customer has no cylinders either - safe to delete directly
        await _customerRepository.DeleteAsync(customer, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
