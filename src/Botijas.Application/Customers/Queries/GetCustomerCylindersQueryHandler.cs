using Botijas.Application.Common;
using Botijas.Domain.Repositories;

namespace Botijas.Application.Customers.Queries;

public record GetCustomerCylindersQuery(Guid CustomerId);

public class CustomerCylindersDto
{
    public Guid CustomerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string PhoneType { get; set; } = string.Empty;
    public List<CustomerCylinderDto> Cylinders { get; set; } = new();
    public List<CustomerOrderDto> Orders { get; set; } = new();
}

public class CustomerCylinderDto
{
    public Guid CylinderId { get; set; }
    public long SequentialNumber { get; set; }
    public string? LabelToken { get; set; }
    public string State { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Guid OrderId { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public string FulfillmentMethod { get; set; } = string.Empty;
    public bool RefillPaid { get; set; }
    public bool ShippingPaid { get; set; }
    public DateTime OrderCreatedAt { get; set; }
    public DateTime? OrderCompletedAt { get; set; }
    public DateTime? OrderCancelledAt { get; set; }
    public string? OrderCancellationNotes { get; set; }
    public List<CustomerCylinderHistoryItemDto> History { get; set; } = new();
}

public class CustomerOrderDto
{
    public Guid OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string FulfillmentMethod { get; set; } = string.Empty;
    public bool RefillPaid { get; set; }
    public bool ShippingPaid { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationNotes { get; set; }
    public List<CustomerCylinderDto> Cylinders { get; set; } = new();
}

public class CustomerCylinderHistoryItemDto
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; }
}

public class GetCustomerCylindersQueryHandler
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ICylinderRepository _cylinderRepository;
    private readonly IRefillOrderRepository _orderRepository;
    private readonly ICylinderHistoryRepository _historyRepository;

    public GetCustomerCylindersQueryHandler(
        ICustomerRepository customerRepository,
        ICylinderRepository cylinderRepository,
        IRefillOrderRepository orderRepository,
        ICylinderHistoryRepository historyRepository)
    {
        _customerRepository = customerRepository;
        _cylinderRepository = cylinderRepository;
        _orderRepository = orderRepository;
        _historyRepository = historyRepository;
    }

    public async Task<Result<CustomerCylindersDto>> Handle(GetCustomerCylindersQuery query, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.FindByIdAsync(query.CustomerId, cancellationToken);
        if (customer == null)
            return Result<CustomerCylindersDto>.Failure("Cliente não encontrado");

        var cylinderDtos = new List<CustomerCylinderDto>();
        var cylinders = await _cylinderRepository.FindByCustomerIdAsync(query.CustomerId, cancellationToken);
        var allOrders = await _orderRepository.FindAllByCustomerAsync(query.CustomerId, cancellationToken);
        var ordersById = allOrders.ToDictionary(o => o.OrderId);
        var currentOrders = await _orderRepository.FindCurrentCylinderOrdersByCustomerAsync(query.CustomerId, cancellationToken);
        var currentOrdersByCylinderId = currentOrders.ToDictionary(x => x.CylinderId);
        var historiesByCylinderId = new Dictionary<Guid, List<CustomerCylinderHistoryItemDto>>();

        foreach (var cylinder in cylinders.OrderBy(c => c.SequentialNumber))
        {
            var history = await _historyRepository.GetByCylinderIdAsync(cylinder.CylinderId, cancellationToken);
            historiesByCylinderId[cylinder.CylinderId] = history.Select(h => new CustomerCylinderHistoryItemDto
            {
                Id = h.Id,
                EventType = h.EventType.ToString(),
                Details = h.Details,
                Timestamp = h.Timestamp
            }).ToList();

            if (!currentOrdersByCylinderId.TryGetValue(cylinder.CylinderId, out var currentOrder))
            {
                continue;
            }

            cylinderDtos.Add(new CustomerCylinderDto
            {
                CylinderId = cylinder.CylinderId,
                SequentialNumber = cylinder.SequentialNumber,
                LabelToken = cylinder.LabelToken?.Value,
                State = cylinder.State.ToString(),
                CreatedAt = cylinder.CreatedAt,
                OrderId = currentOrder.OrderId,
                OrderStatus = currentOrder.OrderStatus.ToString(),
                FulfillmentMethod = currentOrder.FulfillmentMethod.ToString(),
                RefillPaid = currentOrder.RefillPaid,
                ShippingPaid = currentOrder.ShippingPaid,
                OrderCreatedAt = currentOrder.OrderCreatedAt,
                OrderCompletedAt = currentOrder.CompletedAt,
                OrderCancelledAt = currentOrder.CancelledAt,
                OrderCancellationNotes = currentOrder.CancellationNotes,
                History = historiesByCylinderId[cylinder.CylinderId]
            });
        }

        var cylindersById = cylinders.ToDictionary(c => c.CylinderId);
        var orderDtos = allOrders.Select(order => new CustomerOrderDto
        {
            OrderId = order.OrderId,
            Status = order.Status.ToString(),
            FulfillmentMethod = order.FulfillmentMethod.ToString(),
            RefillPaid = order.RefillPaid,
            ShippingPaid = order.ShippingPaid,
            CreatedAt = order.CreatedAt,
            CompletedAt = order.CompletedAt,
            CancelledAt = order.CancelledAt,
            CancellationNotes = order.CancellationNotes,
            Cylinders = order.Cylinders
                .Select(cylinderRef =>
                {
                    if (!cylindersById.TryGetValue(cylinderRef.CylinderId, out var cylinder))
                    {
                        return null;
                    }

                    historiesByCylinderId.TryGetValue(cylinder.CylinderId, out var history);

                    return new CustomerCylinderDto
                    {
                        CylinderId = cylinder.CylinderId,
                        SequentialNumber = cylinder.SequentialNumber,
                        LabelToken = cylinder.LabelToken?.Value,
                        State = cylinderRef.State.ToString(),
                        CreatedAt = cylinder.CreatedAt,
                        OrderId = order.OrderId,
                        OrderStatus = order.Status.ToString(),
                        FulfillmentMethod = order.FulfillmentMethod.ToString(),
                        RefillPaid = order.RefillPaid,
                        ShippingPaid = order.ShippingPaid,
                        OrderCreatedAt = order.CreatedAt,
                        OrderCompletedAt = order.CompletedAt,
                        OrderCancelledAt = order.CancelledAt,
                        OrderCancellationNotes = order.CancellationNotes,
                        History = history ?? new List<CustomerCylinderHistoryItemDto>()
                    };
                })
                .Where(dto => dto != null)
                .Select(dto => dto!)
                .OrderBy(dto => dto.SequentialNumber)
                .ToList()
        }).ToList();

        var dto = new CustomerCylindersDto
        {
            CustomerId = customer.CustomerId,
            Name = customer.Name,
            Phone = customer.Phone.Value,
            PhoneType = customer.PhoneType.ToString(),
            Cylinders = cylinderDtos,
            Orders = orderDtos
        };

        return Result<CustomerCylindersDto>.Success(dto);
    }
}
