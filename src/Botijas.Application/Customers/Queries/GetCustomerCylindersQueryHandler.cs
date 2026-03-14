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
    public List<CustomerCylinderHistoryItemDto> History { get; set; } = new();
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
        var currentOrders = await _orderRepository.FindCurrentCylinderOrdersByCustomerAsync(query.CustomerId, cancellationToken);
        var currentOrdersByCylinderId = currentOrders.ToDictionary(x => x.CylinderId);

        foreach (var cylinder in cylinders.OrderBy(c => c.SequentialNumber))
        {
            var history = await _historyRepository.GetByCylinderIdAsync(cylinder.CylinderId, cancellationToken);
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
                History = history.Select(h => new CustomerCylinderHistoryItemDto
                {
                    Id = h.Id,
                    EventType = h.EventType.ToString(),
                    Details = h.Details,
                    Timestamp = h.Timestamp
                }).ToList()
            });
        }

        var dto = new CustomerCylindersDto
        {
            CustomerId = customer.CustomerId,
            Name = customer.Name,
            Phone = customer.Phone.Value,
            PhoneType = customer.PhoneType.ToString(),
            Cylinders = cylinderDtos
        };

        return Result<CustomerCylindersDto>.Success(dto);
    }
}
