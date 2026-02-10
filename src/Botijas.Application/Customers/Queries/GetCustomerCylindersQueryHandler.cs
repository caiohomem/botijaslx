using Botijas.Application.Common;
using Botijas.Domain.Repositories;

namespace Botijas.Application.Customers.Queries;

public record GetCustomerCylindersQuery(Guid CustomerId);

public class CustomerCylindersDto
{
    public Guid CustomerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
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

        var orders = await _orderRepository.FindAllByCustomerAsync(query.CustomerId, cancellationToken);

        var cylinderDtos = new List<CustomerCylinderDto>();

        foreach (var order in orders)
        {
            var cylinders = await _cylinderRepository.FindByOrderIdAsync(order.OrderId, cancellationToken);

            foreach (var cylinder in cylinders)
            {
                var history = await _historyRepository.GetByCylinderIdAsync(cylinder.CylinderId, cancellationToken);

                cylinderDtos.Add(new CustomerCylinderDto
                {
                    CylinderId = cylinder.CylinderId,
                    SequentialNumber = cylinder.SequentialNumber,
                    LabelToken = cylinder.LabelToken?.Value,
                    State = cylinder.State.ToString(),
                    CreatedAt = cylinder.CreatedAt,
                    OrderId = order.OrderId,
                    OrderStatus = order.Status.ToString(),
                    History = history.Select(h => new CustomerCylinderHistoryItemDto
                    {
                        EventType = h.EventType.ToString(),
                        Details = h.Details,
                        Timestamp = h.Timestamp
                    }).ToList()
                });
            }
        }

        var dto = new CustomerCylindersDto
        {
            CustomerId = customer.CustomerId,
            Name = customer.Name,
            Phone = customer.Phone.Value,
            Cylinders = cylinderDtos
        };

        return Result<CustomerCylindersDto>.Success(dto);
    }
}
