using Botijas.Application.Common;
using Botijas.Domain.Entities;
using Botijas.Domain.Repositories;

namespace Botijas.Application.Pickup.Queries;

public class GetReadyForPickupQueryHandler
{
    private readonly IRefillOrderRepository _orderRepository;
    private readonly ICylinderRepository _cylinderRepository;
    private readonly ICustomerRepository _customerRepository;

    public GetReadyForPickupQueryHandler(
        IRefillOrderRepository orderRepository,
        ICylinderRepository cylinderRepository,
        ICustomerRepository customerRepository)
    {
        _orderRepository = orderRepository;
        _cylinderRepository = cylinderRepository;
        _customerRepository = customerRepository;
    }

    public async Task<Result<List<PickupOrderDto>>> Handle(GetReadyForPickupQuery query, CancellationToken cancellationToken)
    {
        // Buscar pedidos prontos para recolha
        var orders = await _orderRepository.FindReadyForPickupAsync(null, cancellationToken);

        var result = new List<PickupOrderDto>();

        foreach (var order in orders)
        {
            // Buscar cliente
            var customer = await _customerRepository.FindByIdAsync(order.CustomerId, cancellationToken);
            if (customer == null) continue;

            // Filtrar por busca de cliente se fornecido
            if (!string.IsNullOrWhiteSpace(query.CustomerSearch))
            {
                var searchLower = query.CustomerSearch.ToLower();
                if (!customer.Name.ToLower().Contains(searchLower) && 
                    !customer.Phone.Value.Contains(searchLower))
                {
                    continue;
                }
            }

            // Buscar botijas do pedido
            var cylinders = await _cylinderRepository.FindByOrderIdAsync(order.OrderId, cancellationToken);

            var cylinderDtos = cylinders.Select(c => new PickupCylinderDto
            {
                CylinderId = c.CylinderId,
                SequentialNumber = c.SequentialNumber,
                LabelToken = c.LabelToken?.Value,
                State = c.State.ToString(),
                IsDelivered = c.State == CylinderState.Delivered
            }).ToList();

            result.Add(new PickupOrderDto
            {
                OrderId = order.OrderId,
                CustomerId = order.CustomerId,
                CustomerName = customer.Name,
                CustomerPhone = customer.Phone.Value,
                Status = order.Status.ToString(),
                CreatedAt = order.CreatedAt,
                NotifiedAt = order.NotifiedAt,
                NeedsNotification = order.NeedsNotification,
                TotalCylinders = cylinders.Count,
                DeliveredCylinders = cylinders.Count(c => c.State == CylinderState.Delivered),
                Cylinders = cylinderDtos
            });
        }

        // Ordenar por data de criação (mais antigos primeiro)
        result = result.OrderBy(o => o.CreatedAt).ToList();

        return Result<List<PickupOrderDto>>.Success(result);
    }
}
