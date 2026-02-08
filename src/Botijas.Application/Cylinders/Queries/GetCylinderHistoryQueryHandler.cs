using Botijas.Application.Common;
using Botijas.Domain.Repositories;

namespace Botijas.Application.Cylinders.Queries;

public class GetCylinderHistoryQueryHandler
{
    private readonly ICylinderRepository _cylinderRepository;
    private readonly ICylinderHistoryRepository _historyRepository;
    private readonly IRefillOrderRepository _orderRepository;
    private readonly ICustomerRepository _customerRepository;

    public GetCylinderHistoryQueryHandler(
        ICylinderRepository cylinderRepository,
        ICylinderHistoryRepository historyRepository,
        IRefillOrderRepository orderRepository,
        ICustomerRepository customerRepository)
    {
        _cylinderRepository = cylinderRepository;
        _historyRepository = historyRepository;
        _orderRepository = orderRepository;
        _customerRepository = customerRepository;
    }

    public async Task<Result<CylinderHistoryDto>> Handle(
        GetCylinderHistoryQuery query,
        CancellationToken cancellationToken)
    {
        // Buscar botija
        var cylinder = await _cylinderRepository.FindByIdAsync(query.CylinderId, cancellationToken);
        if (cylinder == null)
        {
            return Result<CylinderHistoryDto>.Failure("Botija não encontrada");
        }

        // Buscar histórico
        var history = await _historyRepository.GetByCylinderIdAsync(query.CylinderId, cancellationToken);

        // Buscar pedido atual (se houver)
        var order = await _orderRepository.FindByCylinderIdAsync(query.CylinderId, cancellationToken);
        string? customerName = null;
        string? customerPhone = null;

        if (order != null)
        {
            var customer = await _customerRepository.FindByIdAsync(order.CustomerId, cancellationToken);
            if (customer != null)
            {
                customerName = customer.Name;
                customerPhone = customer.Phone.Value;
            }
        }

        var historyItems = history.Select(h => new CylinderHistoryItemDto
        {
            EventType = h.EventType.ToString(),
            Details = h.Details,
            OrderId = h.OrderId,
            Timestamp = h.Timestamp
        }).ToList();

        return Result<CylinderHistoryDto>.Success(new CylinderHistoryDto
        {
            CylinderId = cylinder.CylinderId,
            LabelToken = cylinder.LabelToken?.Value,
            State = cylinder.State.ToString(),
            CreatedAt = cylinder.CreatedAt,
            CurrentOrderId = order?.OrderId,
            CurrentOrderStatus = order?.Status.ToString(),
            CustomerName = customerName,
            CustomerPhone = customerPhone,
            History = historyItems
        });
    }
}

public class CylinderHistoryDto
{
    public Guid CylinderId { get; set; }
    public string? LabelToken { get; set; }
    public string State { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Guid? CurrentOrderId { get; set; }
    public string? CurrentOrderStatus { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public List<CylinderHistoryItemDto> History { get; set; } = new();
}

public class CylinderHistoryItemDto
{
    public string EventType { get; set; } = string.Empty;
    public string? Details { get; set; }
    public Guid? OrderId { get; set; }
    public DateTime Timestamp { get; set; }
}
