using Botijas.Application.Common;
using Botijas.Domain.Repositories;
using Botijas.Domain.ValueObjects;

namespace Botijas.Application.Cylinders.Queries;

public class GetCylinderByTokenQueryHandler
{
    private readonly ICylinderRepository _cylinderRepository;
    private readonly ICylinderHistoryRepository _historyRepository;
    private readonly IRefillOrderRepository _orderRepository;
    private readonly ICustomerRepository _customerRepository;

    public GetCylinderByTokenQueryHandler(
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
        GetCylinderByTokenQuery query,
        CancellationToken cancellationToken)
    {
        // Buscar botija por token
        var labelToken = LabelToken.Create(query.QrToken);
        var cylinder = await _cylinderRepository.FindByLabelTokenAsync(labelToken, cancellationToken);
        
        if (cylinder == null)
        {
            return Result<CylinderHistoryDto>.Failure("Botija não encontrada com este QR");
        }

        // Buscar histórico
        var history = await _historyRepository.GetByCylinderIdAsync(cylinder.CylinderId, cancellationToken);

        // Buscar pedido atual
        var order = await _orderRepository.FindByCylinderIdAsync(cylinder.CylinderId, cancellationToken);
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
