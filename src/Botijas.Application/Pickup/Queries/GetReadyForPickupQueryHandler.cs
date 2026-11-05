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

        // Data em que cada pedido ficou pronto = última botija enchida (evento MarkedReady).
        // Uma única query agregada para todos os pedidos.
        var readyAtByOrder = await _orderRepository.GetReadyAtByOrderAsync(cancellationToken);

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

            var cylinders = await _cylinderRepository.FindByOrderIdAsync(order.OrderId, cancellationToken);

            // Corrige pedidos presos: todas as botijas já prontas mas o estado ficou Open
            // (marcação em paralelo), ou todas entregues mas o pedido não fechou.
            var allDelivered = cylinders.Count > 0 && cylinders.All(c => c.State == CylinderState.Delivered);
            var allReady = cylinders.Count > 0 && cylinders.All(c => RefillOrder.IsCylinderReadyForPickup(c.State));
            if ((order.Status == RefillOrderStatus.Open && allReady) ||
                (order.Status == RefillOrderStatus.ReadyForPickup && allDelivered))
            {
                order.RecalculateStatus(cylinders);
                if (order.Status is RefillOrderStatus.Completed or RefillOrderStatus.Cancelled)
                {
                    await _orderRepository.SaveChangesAsync(cancellationToken);
                    continue;
                }

                await _orderRepository.SaveChangesAsync(cancellationToken);
            }

            var cylinderDtos = cylinders.Select(c => new PickupCylinderDto
            {
                CylinderId = c.CylinderId,
                SequentialNumber = c.SequentialNumber,
                LabelToken = c.LabelToken?.Value,
                State = c.State.ToString(),
                OccurrenceNotes = c.OccurrenceNotes,
                IsDelivered = c.State == CylinderState.Delivered,
                OrderId = order.OrderId,
            }).ToList();

            result.Add(new PickupOrderDto
            {
                OrderId = order.OrderId,
                CustomerId = order.CustomerId,
                CustomerName = customer.Name,
                CustomerPhone = customer.Phone.Value,
                CustomerPhoneType = customer.PhoneType.ToString(),
                Status = order.Status.ToString(),
                FulfillmentMethod = order.FulfillmentMethod.ToString(),
                RefillPaid = order.RefillPaid,
                ShippingPaid = order.ShippingPaid,
                CreatedAt = order.CreatedAt,
                ReadyAt = readyAtByOrder.TryGetValue(order.OrderId, out var readyAt) ? readyAt : null,
                NotifiedAt = order.NotifiedAt,
                ShippedAt = order.ShippedAt,
                NeedsNotification = order.NeedsNotification,
                TotalCylinders = cylinders.Count,
                DeliveredCylinders = cylinders.Count(c => c.State == CylinderState.Delivered),
                Cylinders = cylinderDtos
            });
        }

        result = PickupOrderConsolidator.ConsolidateByCustomer(result);

        return Result<List<PickupOrderDto>>.Success(result);
    }
}
